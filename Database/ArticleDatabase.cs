using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    internal sealed partial class ArticleDatabase : IArticleDatabase, IDisposable
    {
        // To help diagnose calls that skipped initialization
        private int initialized = 0;

        private const int CURRENT_DB_VERSION = 1;

        public long UnreadFolderLocalId { get; private set; }
        public long ArchiveFolderLocalId { get; private set; }

        private readonly IDbConnection connection;
        private IChangesDatabase? changesDatabase;

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
            this.changesDatabase = null;

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
            if (state < 1)
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

                // Perform any migrations
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

                // Get default folder database IDs
                using (var wellKnownFolderLocalId = c.CreateCommand(@"
                    SELECT local_id
                    FROM folders
                    WHERE service_id = @well_known_service_id"))
                {
                    wellKnownFolderLocalId.AddParameter("@well_known_service_id", WellKnownFolderIds.Unread);
                    this.UnreadFolderLocalId = (long)wellKnownFolderLocalId.ExecuteScalar();

                    wellKnownFolderLocalId.Parameters.Clear();
                    wellKnownFolderLocalId.AddParameter("@well_known_service_id", WellKnownFolderIds.Archive);
                    this.ArchiveFolderLocalId = (long)wellKnownFolderLocalId.ExecuteScalar();
                }

                Interlocked.Increment(ref this.initialized);
            }

            return Task.Run(OpenAndCreateDatabase);
        }

        /// <inheritdoc/>
        public IChangesDatabase PendingChangesDatabase
        {
            get
            {
                if (this.changesDatabase == null)
                {
                    this.ThrowIfNotReady();
                    this.changesDatabase = PendingChanges.GetPendingChangeDatabase(this.connection);
                }

                return this.changesDatabase;
            }
        }
    }
}
