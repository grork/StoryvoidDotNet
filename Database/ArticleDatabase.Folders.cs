using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// For accessing well known folders from the service that don't have
    /// explicit service IDs.
    /// </summary>
    public static class WellKnownFolderIds
    {
        /// <summary>
        /// Default folder on the service, where new bookmarks are placed by
        /// default.
        /// </summary>
        public const long Unread = -1;

        /// <summary>
        /// Folder for articles that have been archived by the user.
        /// </summary>
        public const long Archive = -2;
    }

    public sealed partial class ArticleDatabase
    {
        /// <inheritdoc/>
        public Task<IList<DatabaseFolder>> GetFoldersAsync()
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            IList<DatabaseFolder> ListFolders()
            {
                using var listFolders = c.CreateCommand("SELECT * FROM folders");
                using var folders = listFolders.ExecuteReader();

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
                using var folderQuery = c.CreateCommand("SELECT * FROM folders WHERE service_id = @serviceId");
                folderQuery.AddParameter("@serviceId", serviceId);

                using var folderRow = folderQuery.ExecuteReader();

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
            using var folderQuery = connection.CreateCommand("SELECT * FROM folders WHERE local_id = @localId");
            folderQuery.AddParameter("@localId", localId);

            using var folderRow = folderQuery.ExecuteReader();

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
                var query = c!.CreateCommand(@"
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
            using var folderWithTitle = connection.CreateCommand("SELECT COUNT(*) FROM folders WHERE title = @title");
            folderWithTitle.AddParameter("@title", title);

            var foldersWithTitleCount = (long)folderWithTitle.ExecuteScalar();

            return (foldersWithTitleCount > 0);
        }

        /// <inheritdoc/>
        public Task<DatabaseFolder> AddKnownFolderAsync(string title, long serviceId, long position, bool syncToMobile)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            long CreateFolder()
            {
                var query = c!.CreateCommand(@"
                    INSERT INTO folders(title, service_id, position, sync_to_mobile)
                    VALUES (@title, @serviceId, @position, @syncToMobile);

                    SELECT last_insert_rowid();
                ");

                query.AddParameter("@title", title);
                query.AddParameter("@serviceId", serviceId);
                query.AddParameter("@position", position);
                query.AddParameter("@syncToMobile", Convert.ToInt64(syncToMobile));

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

        public Task<DatabaseFolder> UpdateFolderAsync(long localId, long serviceId, string title, long position, bool syncToMobile)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            void UpdateFolder()
            {
                var query = c!.CreateCommand(@"
                    UPDATE folders SET
                        service_id = @service_id,
                        title = @title,
                        position = @position,
                        sync_to_mobile = @sync_to_mobile
                    WHERE local_id = @local_id
                ");

                query.AddParameter("@local_id", localId);
                query.AddParameter("@service_id", serviceId);
                query.AddParameter("@title", title);
                query.AddParameter("@position", position);
                query.AddParameter("@sync_to_mobile", Convert.ToInt64(syncToMobile));

                query.ExecuteScalar();
            }

            return Task.Run(() =>
            {
                UpdateFolder();
                return GetFolderByLocalId(c, localId)!;
            });
        }
    }
}