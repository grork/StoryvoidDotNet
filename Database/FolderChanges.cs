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
        ");

        query.AddParameter("@localId", localFolderId);

        try
        {
            query.ExecuteScalar();
            return GetPendingFolderAddById(c, localFolderId)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            throw new FolderNotFoundException(localFolderId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new DuplicatePendingFolderAddException(localFolderId);
        }
    }

    private static PendingFolderAdd? GetPendingFolderAddById(IDbConnection connection, long localFolderId)
    {
        using var query = connection.CreateCommand(@"
            SELECT local_id, title
            FROM folder_adds_with_folder_information
            WHERE local_id = @localId
        ");

        query.AddParameter("@localId", localFolderId);

        PendingFolderAdd? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderAdd.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc/>
    public PendingFolderAdd? GetPendingFolderAdd(long localFolderId)
    {
        var c = this.connection;
        return GetPendingFolderAddById(c, localFolderId);
    }

    /// <inheritdoc/>
    public void RemovePendingFolderAdd(long localFolderId)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
            DELETE FROM folder_adds
            WHERE local_id = @localId
        ");

        query.AddParameter("@localId", localFolderId);

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
        ");

        query.AddParameter("@serviceId", serviceId);
        query.AddParameter("@title", title);

        try
        {
            query.ExecuteScalar();
            return GetPendingFolderDeleteByServiceId(c, serviceId)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && (ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY) || ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_UNIQUE)
        {
            throw new DuplicatePendingFolderDeleteException(serviceId);
        }
    }

    /// <inheritdoc/>
    public void RemovePendingFolderDelete(long serviceId)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
            DELETE FROM folder_deletes
            WHERE service_id = @serviceId
        ");

        query.AddParameter("@serviceId", serviceId);

        query.ExecuteNonQuery();
    }

    private static PendingFolderDelete? GetPendingFolderDeleteByServiceId(IDbConnection connection, long serviceId)
    {
        using var query = connection.CreateCommand(@"
            SELECT service_id, title
            FROM folder_deletes
            WHERE service_id = @serviceId
        ");

        query.AddParameter("@serviceId", serviceId);

        PendingFolderDelete? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderDelete.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc/>
    public PendingFolderDelete? GetPendingFolderDelete(long serviceId)
    {
        var c = this.connection;
        return GetPendingFolderDeleteByServiceId(c, serviceId);
    }

    /// <inheritdoc/>
    public PendingFolderDelete? GetPendingFolderDeleteByTitle(string title)
    {
        var c = this.connection;
        using var query = connection.CreateCommand(@"
            SELECT service_id, title
            FROM folder_deletes
            WHERE title = @title
        ");

        query.AddParameter("@title", title);

        PendingFolderDelete? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingFolderDelete.FromRow(row);
        }

        return result;
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