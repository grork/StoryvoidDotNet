using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    sealed partial class ArticleDatabase
    {
        /// <inheritdoc/>
        public Task<IList<DatabaseFolder>> ListAllFoldersAsync()
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            IList<DatabaseFolder> ListFolders()
            {
                using var query = c.CreateCommand(@"
                    SELECT *
                    FROM folders
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

            return Task.Run(ListFolders);
        }

        /// <inheritdoc/>
        public Task<DatabaseFolder?> GetFolderByServiceIdAsync(long serviceId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            DatabaseFolder? GetFolder()
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

            return Task.Run(GetFolder);
        }

        /// <inheritdoc/>
        public Task<DatabaseFolder?> GetFolderByLocalIdAsync(long localId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            return Task.Run(() => GetFolderByLocalId(c, localId));
        }

        private static DatabaseFolder? GetFolderByLocalId(IDbConnection connection, long localId)
        {
            using var query = connection.CreateCommand(@"
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
        public Task<DatabaseFolder> CreateFolderAsync(string title)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            long CreateFolder()
            {
                using var query = c!.CreateCommand(@"
                    INSERT INTO folders(title)
                    VALUES (@title);

                    SELECT last_insert_rowid();
                ");

                query.AddParameter("@title", title);
                var rowId = (long)query.ExecuteScalar();

                return rowId;
            }

            return Task.Run(() =>
            {
                if (FolderWithTitleExists(c, title))
                {
                    throw new DuplicateNameException($"Folder with name '{title}' already exists");
                }

                var newFolderRowId = CreateFolder();
                return GetFolderByLocalId(c, newFolderRowId)!;
            });
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
        public Task<DatabaseFolder> AddKnownFolderAsync(string title, long serviceId, long position, bool shouldSync)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            long CreateFolder()
            {
                using var query = c!.CreateCommand(@"
                    INSERT INTO folders(title, service_id, position, should_sync)
                    VALUES (@title, @serviceId, @position, @shouldSync);

                    SELECT last_insert_rowid();
                ");

                query.AddParameter("@title", title);
                query.AddParameter("@serviceId", serviceId);
                query.AddParameter("@position", position);
                query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

                var rowId = (long)query.ExecuteScalar();

                return rowId;
            }

            return Task.Run(() =>
            {
                if (FolderWithTitleExists(c, title))
                {
                    throw new DuplicateNameException($"Folder with name '{title}' already exists");
                }

                var newFolderRowId = CreateFolder();
                return GetFolderByLocalId(c, newFolderRowId)!;
            });
        }

        public Task<DatabaseFolder> UpdateFolderAsync(long localId, long serviceId, string title, long position, bool shouldSync)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            void UpdateFolder()
            {
                using var query = c!.CreateCommand(@"
                    UPDATE folders SET
                        service_id = @serviceId,
                        title = @title,
                        position = @position,
                        should_sync = @shouldSync
                    WHERE local_id = @localId
                ");

                query.AddParameter("@localId", localId);
                query.AddParameter("@serviceId", serviceId);
                query.AddParameter("@title", title);
                query.AddParameter("@position", position);
                query.AddParameter("@shouldSync", Convert.ToInt64(shouldSync));

                var impactedRows = query.ExecuteNonQuery();
                if(impactedRows < 1)
                {
                    throw new FolderNotFoundException(localId);
                }
            }

            return Task.Run(() =>
            {
                UpdateFolder();
                return GetFolderByLocalId(c, localId)!;
            });
        }

        public Task DeleteFolderAsync(long localFolderId)
        {
            if(localFolderId == this.UnreadFolderLocalId)
            {
                throw new InvalidOperationException("Deleting the Unread folder is not allowed");
            }

            if(localFolderId == this.ArchiveFolderLocalId)
            {
                throw new InvalidOperationException("Deleting the Archive folder is not allowed");
            }

            var c = this.connection;

            // Remove any bookmark-folder-pairs
            void DeleteBookmarkFolderPairs()
            {
                using var query = c!.CreateCommand(@"
                    DELETE FROM bookmark_to_folder
                    WHERE local_folder_id = @local_folder_id
                ");

                query.AddParameter("@local_folder_id", localFolderId);

                query.ExecuteNonQuery();
            }


            // Delete the folder
            void DeleteFolder()
            {
                using var query = c!.CreateCommand(@"
                    DELETE FROM folders
                    WHERE local_id = @local_id
                ");

                query.AddParameter("@local_id", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                DeleteBookmarkFolderPairs();
                DeleteFolder();
            });
        }
    }
}