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
    /// Database store for Bookmarks &amp; Folders from the Instapaper Service
    /// </summary>
    public interface IArticleDatabase : IDisposable
    {
        /// <summary>
        /// Gets all locally known folders, including the default
        /// folders (Unread, Archive)
        /// </summary>
        /// <returns>List of folders</returns>
        Task<IList<DatabaseFolder>> GetFoldersAsync();

        /// <summary>
        /// Gets a specific folder using it's service ID
        /// </summary>
        /// <param name="serviceId">Service ID of the folder</param>
        /// <returns>Folder if found, null otherwise</returns>
        Task<DatabaseFolder?> GetFolderByServiceIdAsync(long serviceId);

        /// <summary>
        /// Gets a specific folder using it's local ID
        /// </summary>
        /// <param name="serviceId">Local ID of the folder</param>
        /// <returns>Folder if found, null otherwise</returns>
        Task<DatabaseFolder?> GetFolderByLocalIdAsync(long localId);

        /// <summary>
        /// Creates a new, local folder
        /// </summary>
        /// <param name="title">Title of the folder</param>
        /// <returns>A new folder instance</returns>
        Task<DatabaseFolder> CreateFolderAsync(string title);

        /// <summary>
        /// Adds a known folder to the database. This intended to be used when
        /// you have a fully-filled out folder from the service.
        /// </summary>
        /// <param name="title">Title for this folder</param>
        /// <param name="serviceId">The ID of the folder on the service</param>
        /// <param name="position">The relative order of the folder</param>
        /// <param name="syncToMobile">Should the folder by synced</param>
        /// <returns>The folder after being added to the database</returns>
        Task<DatabaseFolder> AddKnownFolderAsync(string title, long serviceId, long position, bool syncToMobile);

        /// <summary>
        /// Updates the data of a folder with the supplied Local ID. All fields
        /// must be supplied.
        /// </summary>
        /// <param name="localId">Item to update</param>
        /// <param name="serviceId">Service ID to set</param>
        /// <param name="title">Title to set</param>
        /// <param name="position">Position to set</param>
        /// <param name="syncToMobile">Should be synced</param>
        /// <returns>Updated folder</returns>
        Task<DatabaseFolder> UpdateFolderAsync(long localId, long serviceId, string title, long position, bool syncToMobile);
    }

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

    public sealed class ArticleDatabase : IArticleDatabase, IDisposable
    {
        private const int CURRENT_DB_VERSION = 1;

        // To help diagnose calls that skipped initialization
        private int initialized = 0;

        private readonly IDbConnection connection; 
        public ArticleDatabase(IDbConnection connection)
        {
            this.connection = connection;
        }

        private bool alreadyDisposed;
        void IDisposable.Dispose()
        {
            if (alreadyDisposed)
            {
                return;
            }

            this.alreadyDisposed = true;
            this.initialized = 0;

            this.connection.Close();
            this.connection.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Checks if we're ready to be used for accessing data, and throws if not
        /// </summary>
        private void ThrowIfNotReady()
        {
            var state = this.initialized;
            if(state < 1)
            {
                throw new InvalidOperationException("Database must be initialized before use");
            }
        }

        /// <summary>
        /// Opens, creates, or migrates the database
        /// </summary>
        /// <returns>Task that completes when the database is ready</returns>
        public Task OpenOrCreateDatabaseAsync()
        {
            var c = this.connection;
            void OpenAndCreateDatabase()
            {
                c.Open();

                using (var checkIfUpdated = c.CreateCommand("PRAGMA user_version"))
                {
                    var databaseVersion = Convert.ToInt32(checkIfUpdated.ExecuteScalar());
                    if (CURRENT_DB_VERSION != databaseVersion)
                    {
                        // Database is not the right version, so do a migration
                        var migrateQueryText = File.ReadAllText("migrations/v0-to-v1.sql");

                        using var migrate = c.CreateCommand(migrateQueryText);
                        var result = migrate.ExecuteNonQuery();
                        if (result == 0)
                        {
                            throw new InvalidOperationException("Unable to create database");
                        }
                    }
                }

                Interlocked.Increment(ref this.initialized);
            }

            return Task.Run(OpenAndCreateDatabase);
        }
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

                while(folders.Read())
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
