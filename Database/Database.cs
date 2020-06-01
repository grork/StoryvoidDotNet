using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Database store for Bookmarks &amp; Folders from the Instapaper Service
    /// </summary>
    public interface IInstapaperDatabase : IDisposable
    {
        /// <summary>
        /// Opens, creates, or migrates the database
        /// </summary>
        /// <returns>Task that completes when the database is ready</returns>
        Task OpenOrCreateDatabaseAsync();
    }

    public sealed class Database : IInstapaperDatabase, IDisposable
    {
        private const int CURRENT_DB_VERSION = 1;

        private IDbConnection connection; 
        public Database(IDbConnection connection)
        {
            this.connection = connection;
        }

        /// <inheritdoc />
        public Task OpenOrCreateDatabaseAsync()
        {
            var c = this.connection;
            void OpenAndCreateDatabase()
            {
                c.Open();

                using (var checkIfUpdated = c.CreateCommand("PRAGMA user_version"))
                {
                    var databaseVersion = Convert.ToInt32(checkIfUpdated.ExecuteScalar());
                    if (CURRENT_DB_VERSION == databaseVersion)
                    {
                        // We're on the right version of the database, lets go!
                        return;
                    }
                }

                var migrateQueryText = File.ReadAllText("migrations/v0-to-v1.sql");
                using (var migrate = c.CreateCommand(migrateQueryText))
                {
                    var result = migrate.ExecuteNonQuery();
                    Debug.Assert(result == 0);
                }
            }

            return Task.Run(OpenAndCreateDatabase);
        }

        private bool alreadyDisposed;
        void IDisposable.Dispose()
        {
            if(alreadyDisposed)
            {
                return;
            }

            this.alreadyDisposed = true;

            this.connection.Close();
            this.connection.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
