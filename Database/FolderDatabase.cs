using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

internal sealed class FolderDatabase : IFolderDatabaseWithTransactionEvents
{
    private IDbConnection connection;

    public event EventHandler<string>? FolderAddedWithinTransaction;
    public event EventHandler<DatabaseFolder>? FolderWillBeDeletedWithinTransaction;
    public event EventHandler<DatabaseFolder>? FolderDeletedWithinTransaction;

    public FolderDatabase(IDbConnection connection)
    {
        this.connection = connection;
    }

    /// <inheritdoc/>
    public IList<DatabaseFolder> ListAllFolders()
    {
        var c = this.connection;

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

    /// <inheritdoc/>
    public DatabaseFolder? GetFolderByServiceId(long serviceId)
    {
        var c = this.connection;

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
        var c = this.connection;
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
        var c = this.connection;
        using var query = c.CreateCommand(@"
            SELECT *
            FROM folders
            WHERE title = @title
        ");

        query.AddParameter("@title", title);

        using var folderRow = query.ExecuteReader();

        DatabaseFolder? folder = null;
        if(folderRow.Read())
        {
            folder = DatabaseFolder.FromRow(folderRow);
        }

        return folder;
    }

    /// <inheritdoc/>
    public DatabaseFolder CreateFolder(string title)
    {
        var c = this.connection;
        using var t = c.BeginTransaction();

        if (this.GetFolderByTitle(title) is not null)
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        using var query = c.CreateCommand(@"
            INSERT INTO folders(title)
            VALUES (@title);
        ");

        query.AddParameter("@title", title);
        query.ExecuteScalar();

        this.RaiseFolderAddedWithinTransaction(title);

        t.Commit();

        return GetFolderByTitle(title)!;
    }

    /// <inheritdoc/>
    public DatabaseFolder AddKnownFolder(string title, long serviceId, long position, bool shouldSync)
    {
        var c = this.connection;
        using var t = c.BeginTransaction();

        if (this.GetFolderByTitle(title) is not null)
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        using var query = c.CreateCommand(@"
            INSERT INTO folders(title, service_id, position, should_sync)
            VALUES (@title, @serviceId, @position, @shouldSync);
        ");

        query.AddParameter("@title", title);
        query.AddParameter("@serviceId", serviceId);
        query.AddParameter("@position", position);
        query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

        query.ExecuteScalar();

        var addedFolder = GetFolderByServiceId(serviceId)!;

        t.Commit();

        return addedFolder;
    }

    /// <inheritdoc/>
    public DatabaseFolder UpdateFolder(long localId, long? serviceId, string title, long position, bool shouldSync)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
            UPDATE folders SET
                service_id = @serviceId,
                title = @title,
                position = @position,
                should_sync = @shouldSync
            WHERE local_id = @localId
        ");

        query.AddParameter("@localId", localId);
        if (serviceId != null)
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

        var t = (query.Transaction != null) ? null : c.BeginTransaction();
        if(t is not null)
        {
            query.Transaction = t;
        }

        try
        {
            var impactedRows = query.ExecuteNonQuery();
            if (impactedRows < 1)
            {
                throw new FolderNotFoundException(localId);
            }

            var updatedFolder = GetFolderByLocalId(localId)!;

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

        var c = this.connection;
        using var t = c.BeginTransaction();

        var folder = this.GetFolderByLocalId(localFolderId);
        if(folder is null)
        {
            return;
        }

        this.RaiseFolderWillBeDeletedWithinTransaction(folder);

        // Delete any article-folder-pairs
        using var deleteArticleFolderPairsQuery = c.CreateCommand(@"
            DELETE FROM article_to_folder
            WHERE local_folder_id = @localFolderId
        ");

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

        this.RaiseFolderDeletedWithinTransaction(folder);

        t.Commit();
    }

    #region Event Helpers
    private void RaiseFolderAddedWithinTransaction(string title)
    {
        var handlers = this.FolderAddedWithinTransaction;
        handlers?.Invoke(this, title);
    }

    private void RaiseFolderWillBeDeletedWithinTransaction(DatabaseFolder toBeDeleted)
    {
        var handlers = this.FolderWillBeDeletedWithinTransaction;
        handlers?.Invoke(this, toBeDeleted);
    }

    private void RaiseFolderDeletedWithinTransaction(DatabaseFolder deleted)
    {
        var handlers = this.FolderDeletedWithinTransaction;
        handlers?.Invoke(this, deleted);
    }
    #endregion
}