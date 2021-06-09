using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    /// <inheritdoc />
    public class PendingChanges : IChangesDatabase
    {
        private IDbConnection connection;
        private PendingChanges(IDbConnection connection)
        {
            this.connection = connection;
        }

        /// <inheritdoc/>
        public PendingFolderAdd CreatePendingFolderAdd(long localFolderId)
        {
            var c = this.connection;

            using var query = c.CreateCommand(@"
                INSERT INTO folder_adds(local_id)
                VALUES (@localId);

                SELECT last_insert_rowid();
            ");

            query.AddParameter("@localId", localFolderId);
            var changeId = (long)query.ExecuteScalar();

            return GetPendingFolderAddById(c, changeId)!;
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

        /// <summary>
        /// For the supplied DB connection, get an instance of the Pending
        /// Changes API.
        /// </summary>
        /// <param name="connection">
        /// The opened DB Connection to use to access the database.
        /// </param>
        /// <returns>Instance of the the API</returns>
        public static IChangesDatabase GetPendingChangeDatabase(IDbConnection connection)
        {
            if(connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("Database must be opened");
            }

            return new PendingChanges(connection);
        }
    }
}
