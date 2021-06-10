using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid
{
    /// <inheritdoc />
    public class PendingChanges : IChangesDatabase
    {
        public record WellKnownFolderLocalIds(long Unread, long Archive);

        private IDbConnection connection;
        private readonly long unreadFolderLocalId;
        private readonly long archiveFolderLocalId;

        private PendingChanges(IDbConnection connection, WellKnownFolderLocalIds folderLocalIds)
        {
            this.connection = connection;
            this.unreadFolderLocalId = folderLocalIds.Unread;
            this.archiveFolderLocalId = folderLocalIds.Archive;
        }

#region Pending Folder Adds
        /// <inheritdoc/>
        public PendingFolderAdd CreatePendingFolderAdd(long localFolderId)
        {
            if ((localFolderId == this.unreadFolderLocalId)
            || (localFolderId == this.archiveFolderLocalId))
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
            } catch(SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                           && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
            {
                throw new FolderNotFoundException(localFolderId);
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
            if(row.Read())
            {
                result = PendingFolderAdd.FromRow(row);
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<PendingFolderAdd?> GetPendingFolderAddAsync(long changeId)
        {
            var c = this.connection;
            return Task.Run(() => GetPendingFolderAddById(c, changeId));
        }

        /// <inheritdoc/>
        public Task<IList<PendingFolderAdd>> ListPendingFolderAddsAsync()
        {
            var c = this.connection;
            
            IList<PendingFolderAdd> ListPendingFolderAdds()
            {
                using var query = c.CreateCommand(@"
                    SELECT *
                    FROM folder_adds_with_folder_information
                ");

                using var pendingFolderAdds = query.ExecuteReader();

                var result = new List<PendingFolderAdd>();
                while(pendingFolderAdds.Read())
                {
                    var folderAdd = PendingFolderAdd.FromRow(pendingFolderAdds);
                    result.Add(folderAdd);
                }
                
                return result;
            }

            return Task.Run(ListPendingFolderAdds);
        }
#endregion

#region Pending Folder Deletes
        /// <inheritdoc/>
        public PendingFolderDelete CreatePendingFolderDelete(long serviceId, string title)
        {
            if((serviceId == WellKnownFolderIds.Unread)
            || (serviceId == WellKnownFolderIds.Archive))
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

            var changeId = (long)query.ExecuteScalar();

            return GetPendingFolderDeleteById(c, changeId)!;
        }

        private static PendingFolderDelete? GetPendingFolderDeleteById(IDbConnection connection, long changeId)
        {
            using var query = connection.CreateCommand(@"
                SELECT change_id, service_id, title
                FROM folder_deletes
                WHERE change_id = @changeId
            ");

            query.AddParameter("@changeId", changeId);

            PendingFolderDelete? result = null;
            using var row = query.ExecuteReader();
            if(row.Read())
            {
                result = PendingFolderDelete.FromRow(row);
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<PendingFolderDelete?> GetPendingFolderDeleteAsync(long changeId)
        {
            var c = this.connection;
            return Task.Run(() => GetPendingFolderDeleteById(c, changeId));
        }

        /// <inheritdoc/>
        public Task<IList<PendingFolderDelete>> ListPendingFolderDeletesAsync()
        {
            var c = this.connection;
            
            IList<PendingFolderDelete> ListPendingFolderDeletes()
            {
                using var query = c.CreateCommand(@"
                    SELECT *
                    FROM folder_deletes
                ");

                using var pendingFolderDeletes = query.ExecuteReader();

                var result = new List<PendingFolderDelete>();
                while(pendingFolderDeletes.Read())
                {
                    var folderDelete = PendingFolderDelete.FromRow(pendingFolderDeletes);
                    result.Add(folderDelete);
                }
                
                return result;
            }

            return Task.Run(ListPendingFolderDeletes);
        }
#endregion

        /// <summary>
        /// For the supplied DB connection, get an instance of the Pending
        /// Changes API.
        /// </summary>
        /// <param name="connection">
        /// The opened DB Connection to use to access the database.
        /// </param>
        /// <returns>Instance of the the API</returns>
        public static IChangesDatabase GetPendingChangeDatabase(IDbConnection connection, WellKnownFolderLocalIds folderLocalIds)
        {
            if(connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("Database must be opened");
            }

            return new PendingChanges(connection, folderLocalIds);
        }
    }
}
