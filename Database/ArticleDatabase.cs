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
        Task<IList<object>> GetFoldersAsync();
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

        private IDbConnection connection; 
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
        public Task<IList<object>> GetFoldersAsync()
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            IList<Object> ListFolders()
            {
                using var listFolders = c.CreateCommand("SELECT * FROM folders");
                using var folders = listFolders.ExecuteReader();

                var result = new List<Object>();

                while(folders.Read())
                {
                    result.Add(new object());
                }

                return result;
            }

            return Task<IList<Object>>.Run(ListFolders);
        }
    }
}
