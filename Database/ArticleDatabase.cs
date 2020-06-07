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

            bool FolderWithTitleExists()
            {
                using var folderWithTitle = c!.CreateCommand("SELECT COUNT(*) FROM folders WHERE title = @title");
                folderWithTitle.AddParameter("@title", title);

                var foldersWithTitleCount = (long)folderWithTitle.ExecuteScalar();

                return (foldersWithTitleCount > 0);
            }

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
                if (FolderWithTitleExists())
                {
                    throw new DuplicateNameException($"Folder with name '{title}' already exists");
                }

                var newFolderRowId = CreateFolder();
                return GetFolderByLocalId(c, newFolderRowId)!;
            });
        }
    }
}
