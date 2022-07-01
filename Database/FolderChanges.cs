using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

/// <inheritdoc />
internal class FolderChanges : IFolderChangesDatabase
{
    private IDbConnection connection;

    internal FolderChanges(IDbConnection connection)
    {
        this.connection = connection;
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

        return CreatePendingFolderAdd(this.connection, localFolderId);
    }

    private static PendingFolderAdd CreatePendingFolderAdd(IDbConnection c, long localFolderId)
    {
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
    public void DeletePendingFolderAdd(long localFolderId)
    {
        DeletePendingFolderAdd(this.connection, localFolderId);
    }

    private static void DeletePendingFolderAdd(IDbConnection c, long localFolderId)
    {
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
        return ListPendingFolderAdds(this.connection);
    }

    private static IList<PendingFolderAdd> ListPendingFolderAdds(IDbConnection c)
    {
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

        return CreatePendingFolderDelete(this.connection, serviceId, title);
    }

    private static PendingFolderDelete CreatePendingFolderDelete(IDbConnection c, long serviceId, string title)
    {
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
    public void DeletePendingFolderDelete(long serviceId)
    {
        DeletePendingFolderDelete(this.connection, serviceId);
    }

    private static void DeletePendingFolderDelete(IDbConnection c, long serviceId)
    {
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
        return GetPendingFolderDeleteByTitle(this.connection, title);
    }

    private static PendingFolderDelete? GetPendingFolderDeleteByTitle(IDbConnection c, string title)
    {
        using var query = c.CreateCommand(@"
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
        return ListPendingFolderDeletes(this.connection);
    }

    private static IList<PendingFolderDelete> ListPendingFolderDeletes(IDbConnection c)
    {
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
}