using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

internal sealed class FolderDatabase : IFolderDatabaseWithTransactionEvents
{
    private IDbConnection connection;
    private IDatabaseEventSource? eventSource;

    public event WithinTransactionEventHandler<IFolderDatabase, string>? FolderAddedWithinTransaction;
    public event WithinTransactionEventHandler<IFolderDatabase, DatabaseFolder>? FolderWillBeDeletedWithinTransaction;
    public event WithinTransactionEventHandler<IFolderDatabase, DatabaseFolder>? FolderDeletedWithinTransaction;

    public FolderDatabase(IDbConnection connection, IDatabaseEventSource? eventSink = null)
    {
        this.connection = connection;
        this.eventSource = eventSink;
    }

    /// <inheritdoc/>
    public IEnumerable<DatabaseFolder> ListAllFolders()
    {
        return ListAllFolders(this.connection);
    }

    private static IEnumerable<DatabaseFolder> ListAllFolders(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            ORDER BY position ASC
        ");

        using var folders = query.ExecuteReader();

        var result = new List<DatabaseFolder>();

        while (folders.Read())
        {
            var f = DatabaseFolder.FromRow(folders);
            result.Add(f);
        }

        return result;
    }

    public IEnumerable<DatabaseFolder> ListAllUserFolders()
    {
        return ListAllUserFolders(this.connection);
    }

    private static IEnumerable<DatabaseFolder> ListAllUserFolders(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            WHERE local_id <> @unreadId AND local_id <> @archiveId
            ORDER BY position ASC
        ");

        query.AddParameter("@unreadId", WellKnownLocalFolderIds.Unread);
        query.AddParameter("archiveId", WellKnownLocalFolderIds.Archive);

        using var folders = query.ExecuteReader();

        var result = new List<DatabaseFolder>();

        while (folders.Read())
        {
            var f = DatabaseFolder.FromRow(folders);
            result.Add(f);
        }

        return result;
    }

    /// <inheritdoc/>
    public DatabaseFolder? GetFolderByServiceId(long serviceId)
    {
        return GetFolderByServiceId(this.connection, serviceId);
    }

    private static DatabaseFolder? GetFolderByServiceId(IDbConnection c, long serviceId)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            WHERE service_id = @serviceId
        ");

        query.AddParameter("@serviceId", serviceId);

        using var folderRow = query.ExecuteReader();

        DatabaseFolder? folder = null;
        if (folderRow.Read())
        {
            folder = DatabaseFolder.FromRow(folderRow);
        }

        return folder;
    }

    /// <inheritdoc/>
    public DatabaseFolder? GetFolderByLocalId(long localId)
    {
        return GetFolderByLocalId(this.connection, localId);
    }

    private static DatabaseFolder? GetFolderByLocalId(IDbConnection c, long localId)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            WHERE local_id = @localId
        ");

        query.AddParameter("@localId", localId);

        using var folderRow = query.ExecuteReader();

        DatabaseFolder? folder = null;
        if (folderRow.Read())
        {
            folder = DatabaseFolder.FromRow(folderRow);
        }

        return folder;
    }

    /// <inheritdoc />
    public DatabaseFolder? GetFolderByTitle(string title)
    {
        return GetFolderByTitle(this.connection, title);
    }

    private static DatabaseFolder? GetFolderByTitle(IDbConnection c, string title)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            WHERE title = @title
        ");

        query.AddParameter("@title", title);

        using var folderRow = query.ExecuteReader();

        DatabaseFolder? folder = null;
        if (folderRow.Read())
        {
            folder = DatabaseFolder.FromRow(folderRow);
        }

        return folder;
    }

    /// <inheritdoc/>
    public DatabaseFolder CreateFolder(string title)
    {
        var folder = CreateFolder(this.connection, title, this);
        this.eventSource?.RaiseFolderAdded(folder);
        return folder;
    }

    private static DatabaseFolder CreateFolder(IDbConnection c, string title, FolderDatabase eventSource)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO folders(title)
            VALUES (@title);
        ");

        using var t = query.BeginTransactionIfNeeded();

        if (GetFolderByTitle(c, title) is not null)
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        query.AddParameter("@title", title);
        query.ExecuteScalar();

        eventSource.RaiseFolderAddedWithinTransaction(c, title);

        t?.Commit();

        return GetFolderByTitle(c, title)!;
    }

    /// <inheritdoc/>
    public DatabaseFolder AddKnownFolder(string title, long serviceId, long position, bool shouldSync)
    {
        var folder = AddKnownFolder(this.connection, title, serviceId, position, shouldSync);
        this.eventSource?.RaiseFolderAdded(folder);
        return folder;
    }

    private static DatabaseFolder AddKnownFolder(IDbConnection c, string title, long serviceId, long position, bool shouldSync)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO folders(title, service_id, position, should_sync)
            VALUES (@title, @serviceId, @position, @shouldSync);
        ");

        using var t = query.BeginTransactionIfNeeded();

        if (GetFolderByTitle(c, title) is not null)
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        query.AddParameter("@title", title);
        query.AddParameter("@serviceId", serviceId);
        query.AddParameter("@position", position);
        query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

        query.ExecuteScalar();

        var addedFolder = GetFolderByServiceId(c, serviceId)!;

        t?.Commit();

        return addedFolder;
    }

    /// <inheritdoc/>
    public DatabaseFolder UpdateFolder(long localId, long? serviceId, string title, long position, bool shouldSync)
    {
        var folder = UpdateFolder(this.connection, localId, serviceId, title, position, shouldSync);
        this.eventSource?.RaiseFolderUpdated(folder);
        return folder;
    }

    private static DatabaseFolder UpdateFolder(IDbConnection c, long localId, long? serviceId, string title, long position, bool shouldSync)
    {
        using var query = c.CreateCommand(@"
            UPDATE folders SET
                service_id = @serviceId,
                title = @title,
                position = @position,
                should_sync = @shouldSync
            WHERE local_id = @localId
        ");

        var t = query.BeginTransactionIfNeeded();

        query.AddParameter("@localId", localId);
        if (serviceId is not null)
        {
            query.AddParameter("@serviceId", (long)(serviceId!));
        }
        else
        {
            query.AddNull("@serviceId", DbType.Int64);
        }

        query.AddParameter("@title", title);
        query.AddParameter("@position", position);
        query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

        try
        {
            var impactedRows = query.ExecuteNonQuery();
            if (impactedRows < 1)
            {
                throw new FolderNotFoundException(localId);
            }

            var updatedFolder = GetFolderByLocalId(c, localId)!;

            t?.Commit();

            return updatedFolder;
        }
        finally
        {
            t?.Dispose();
        }
    }

    /// <inheritdoc />
    public void DeleteFolder(long localFolderId)
    {
        if (localFolderId == WellKnownLocalFolderIds.Unread)
        {
            throw new InvalidOperationException("Deleting the Unread folder is not allowed");
        }

        if (localFolderId == WellKnownLocalFolderIds.Archive)
        {
            throw new InvalidOperationException("Deleting the Archive folder is not allowed");
        }

        var (wasDeleted, folder) = DeleteFolder(this.connection, localFolderId, this);
        if (wasDeleted)
        {
            this.eventSource?.RaiseFolderDeleted(folder!);
        }
    }

    private static (bool, DatabaseFolder?) DeleteFolder(IDbConnection c, long localFolderId, FolderDatabase eventSource)
    {
        using var deleteArticleFolderPairsQuery = c.CreateCommand(@"
            DELETE FROM article_to_folder
            WHERE local_folder_id = @localFolderId
        ");

        using var t = deleteArticleFolderPairsQuery.BeginTransactionIfNeeded();

        var folder = GetFolderByLocalId(c, localFolderId);
        if (folder is null)
        {
            return (false, null);
        }

        eventSource.RaiseFolderWillBeDeletedWithinTransaction(c, folder);

        // Delete any article-folder-pairs
        deleteArticleFolderPairsQuery.AddParameter("@localFolderId", localFolderId);
        deleteArticleFolderPairsQuery.ExecuteNonQuery();

        // Delete the folder
        using var deleteFolderQuery = c.CreateCommand(@"
            DELETE FROM folders
            WHERE local_id = @localId
        ");

        deleteFolderQuery.AddParameter("@localId", localFolderId);

        try
        {
            deleteFolderQuery.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            throw new InvalidOperationException($"Can't delete folder {localFolderId} that is pending operation. Clear its pending operations first", ex);
        }

        eventSource.RaiseFolderDeletedWithinTransaction(c, folder);

        t?.Commit();

        return (true, folder);
    }

    #region Event Helpers
    private void RaiseFolderAddedWithinTransaction(IDbConnection c, string title)
    {
        var handlers = this.FolderAddedWithinTransaction;
        handlers?.Invoke(this, new(c, title));
    }

    private void RaiseFolderWillBeDeletedWithinTransaction(IDbConnection c, DatabaseFolder toBeDeleted)
    {
        var handlers = this.FolderWillBeDeletedWithinTransaction;
        handlers?.Invoke(this, new(c, toBeDeleted));
    }

    private void RaiseFolderDeletedWithinTransaction(IDbConnection c, DatabaseFolder deleted)
    {
        var handlers = this.FolderDeletedWithinTransaction;
        handlers?.Invoke(this, new(c, deleted));
    }
    #endregion
}