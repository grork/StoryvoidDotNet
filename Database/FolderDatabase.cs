using System.Data;

namespace Codevoid.Storyvoid;

internal sealed class FolderDatabase : IFolderDatabase
{
    private IDbConnection connection;
    private IInstapaperDatabase database;

    public FolderDatabase(IDbConnection connection, IInstapaperDatabase database)
    {
        this.connection = connection;
        this.database = database;
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

    /// <inheritdoc/>
    public DatabaseFolder CreateFolder(string title)
    {
        var c = this.connection;

        if (FolderWithTitleExists(c, title))
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        using var query = c.CreateCommand(@"
                INSERT INTO folders(title)
                VALUES (@title);

                SELECT last_insert_rowid();
            ");

        query.AddParameter("@title", title);
        var rowId = (long)query.ExecuteScalar();

        return GetFolderByLocalId(rowId)!;
    }

    private static bool FolderWithTitleExists(IDbConnection connection, string title)
    {
        using var query = connection.CreateCommand(@"
                SELECT COUNT(*)
                FROM folders
                WHERE title = @title
            ");

        query.AddParameter("@title", title);

        var foldersWithTitleCount = (long)query.ExecuteScalar();

        return (foldersWithTitleCount > 0);
    }

    /// <inheritdoc/>
    public DatabaseFolder AddKnownFolder(string title, long serviceId, long position, bool shouldSync)
    {
        var c = this.connection;

        if (FolderWithTitleExists(c, title))
        {
            throw new DuplicateNameException($"Folder with name '{title}' already exists");
        }

        using var query = c.CreateCommand(@"
                INSERT INTO folders(title, service_id, position, should_sync)
                VALUES (@title, @serviceId, @position, @shouldSync);

                SELECT last_insert_rowid();
            ");

        query.AddParameter("@title", title);
        query.AddParameter("@serviceId", serviceId);
        query.AddParameter("@position", position);
        query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

        var rowId = (long)query.ExecuteScalar();

        return GetFolderByLocalId(rowId)!;
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
            query.AddNull(@"serviceId", DbType.Int64);
        }

        query.AddParameter("@title", title);
        query.AddParameter("@position", position);
        query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

        var impactedRows = query.ExecuteNonQuery();
        if (impactedRows < 1)
        {
            throw new FolderNotFoundException(localId);
        }

        return GetFolderByLocalId(localId)!;
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
        var changesDB = this.database.ChangesDatabase;

        if (changesDB.GetPendingFolderAddByLocalFolderId(localFolderId) != null)
        {
            throw new InvalidOperationException($"Folder {localFolderId} had a pending folder add");
        }

        // Remove any article-folder-pairs
        using var removeArticleFolderPairsQuery = c.CreateCommand(@"
                DELETE FROM article_to_folder
                WHERE local_folder_id = @localFolderId
            ");

        removeArticleFolderPairsQuery.AddParameter("@localFolderId", localFolderId);

        removeArticleFolderPairsQuery.ExecuteNonQuery();

        // Delete the folder
        using var deleteFolderQuery = c.CreateCommand(@"
                DELETE FROM folders
                WHERE local_id = @localId
            ");

        deleteFolderQuery.AddParameter("@localId", localFolderId);

        deleteFolderQuery.ExecuteNonQuery();
    }
}