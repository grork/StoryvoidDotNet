using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

/// <inheritdoc />
public class FolderChanges : IFolderChangesDatabase
{
    private IDbConnection connection;
    private IInstapaperDatabase database;

    private FolderChanges(IDbConnection connection, IInstapaperDatabase database)
    {
        this.connection = connection;
        this.database = database;
    }

    #region Pending Folder Adds
    /// <inheritdoc/>
    public PendingFolderAdd CreatePendingFolderAdd(long localFolderId)
    {
        if ((localFolderId == WellKnownLocalFolderIds.Unread)
        || (localFolderId == WellKnownLocalFolderIds.Archive))
        {
            throw new InvalidOperationException("Can't create a folder add for wellknown folders");
        }

        var c = this.connection;

        using var query = c.CreateCommand(@"
                INSERT INTO folder_adds(local_id)
                VALUES (@localId);

                SELECT last_insert_rowid();
            ");

        query.AddParameter("@localId", localFolderId);

        try
        {
            var changeId = (long)query.ExecuteScalar();
            return GetPendingFolderAddById(c, changeId)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            throw new FolderNotFoundException(localFolderId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_UNIQUE)
        {
            throw new DuplicatePendingFolderAdd(localFolderId);
        }
    }

    private static PendingFolderAdd? GetPendingFolderAddById(IDbConnection connection, long changeId)
    {
        using var query = connection.CreateCommand(@"
                SELECT change_id, local_id, title
                FROM folder_adds_with_folder_information
                WHERE change_id = @changeId
            ");

        query.AddParameter("@changeId", changeId);

        PendingFolderAdd? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderAdd.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc/>
    public PendingFolderAdd? GetPendingFolderAdd(long changeId)
    {
        var c = this.connection;
        return GetPendingFolderAddById(c, changeId);
    }

    public PendingFolderAdd? GetPendingFolderAddByLocalFolderId(long localFolderId)
    {
        var c = connection;
        using var query = c.CreateCommand(@"
                SELECT change_id, local_id, title
                FROM folder_adds_with_folder_information
                WHERE local_id = @localFolderId
            ");

        query.AddParameter("@localFolderId", localFolderId);

        PendingFolderAdd? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderAdd.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc/>
    public void RemovePendingFolderAdd(long changeId)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
                DELETE FROM folder_adds
                WHERE change_id = @changeId
            ");

        query.AddParameter("@changeId", changeId);

        query.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public IList<PendingFolderAdd> ListPendingFolderAdds()
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
                SELECT *
                FROM folder_adds_with_folder_information
            ");

        using var pendingFolderAdds = query.ExecuteReader();

        var result = new List<PendingFolderAdd>();
        while (pendingFolderAdds.Read())
        {
            var folderAdd = PendingFolderAdd.FromRow(pendingFolderAdds);
            result.Add(folderAdd);
        }

        return result;
    }
    #endregion

    #region Pending Folder Deletes
    /// <inheritdoc/>
    public PendingFolderDelete CreatePendingFolderDelete(long serviceId, string title)
    {
        if ((serviceId == WellKnownServiceFolderIds.Unread)
        || (serviceId == WellKnownServiceFolderIds.Archive))
        {
            throw new InvalidOperationException("Can't create pending delete for well known folders");
        }
        var c = this.connection;

        using var query = c.CreateCommand(@"
                INSERT INTO folder_deletes(service_id, title)
                VALUES (@serviceId, @title);

                SELECT last_insert_rowid();
            ");

        query.AddParameter("@serviceId", serviceId);
        query.AddParameter("@title", title);

        try
        {
            var changeId = (long)query.ExecuteScalar();
            return GetPendingFolderDeleteByChangeId(c, changeId)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_UNIQUE)
        {
            throw new DuplicatePendingFolderDelete(serviceId);
        }
    }

    /// <inheritdoc/>
    public void RemovePendingFolderDelete(long changeId)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
                DELETE FROM folder_deletes
                WHERE change_id = @changeId
            ");

        query.AddParameter("@changeId", changeId);

        query.ExecuteNonQuery();
    }

    private static PendingFolderDelete? GetPendingFolderDeleteByChangeId(IDbConnection connection, long changeId)
    {
        using var query = connection.CreateCommand(@"
                SELECT change_id, service_id, title
                FROM folder_deletes
                WHERE change_id = @changeId
            ");

        query.AddParameter("@changeId", changeId);

        PendingFolderDelete? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderDelete.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc/>
    public PendingFolderDelete? GetPendingFolderDelete(long changeId)
    {
        var c = this.connection;
        return GetPendingFolderDeleteByChangeId(c, changeId);
    }

    /// <inheritdoc/>
    public IList<PendingFolderDelete> ListPendingFolderDeletes()
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
                SELECT *
                FROM folder_deletes
            ");

        using var pendingFolderDeletes = query.ExecuteReader();

        var result = new List<PendingFolderDelete>();
        while (pendingFolderDeletes.Read())
        {
            var folderDelete = PendingFolderDelete.FromRow(pendingFolderDeletes);
            result.Add(folderDelete);
        }

        return result;
    }
    #endregion

    /// <summary>
    /// For the supplied DB connection, get an instance of the Pending Folder
    /// Changes API.
    /// </summary>
    /// <param name="connection">
    /// The opened DB Connection to use to access the database.
    /// </param>
    /// <returns>Instance of the the API</returns>
    public static IFolderChangesDatabase GetPendingFolderChangeDatabase(IDbConnection connection, IInstapaperDatabase database)
    {
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Database must be opened");
        }

        return new FolderChanges(connection, database);
    }
}