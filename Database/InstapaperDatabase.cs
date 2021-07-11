using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    internal sealed partial class InstapaperDatabase : IInstapaperDatabase, IDisposable
    {
        // To help diagnose calls that skipped initialization
        private int initialized = 0;

        private const int CURRENT_DB_VERSION = 1;

        public long UnreadFolderLocalId { get; private set; }
        public long ArchiveFolderLocalId { get; private set; }

        private readonly IDbConnection connection;
        private IChangesDatabase? changesDatabase;

        public InstapaperDatabase(IDbConnection connection)
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
        /// Checks if the supplied version is the version of the DB we are
        /// are currently expecting
        /// </summary>
        /// <param name="version">Version to compare about</param>
        /// <returns>True if the version exactly matches</returns>
        private static bool IsCurrentDBVersion(int version)
        {
            return (CURRENT_DB_VERSION == version);
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
                    if (!IsCurrentDBVersion(databaseVersion))
                    {
                        // Database is not the right version, so do a migration
                        var migrateQueryText = File.ReadAllText("migrations/v0-to-v1.sql");

                        using var migrate = c.CreateCommand(migrateQueryText);
                        var result = migrate.ExecuteNonQuery();
                        if (result == 0)
                        {
                            throw new InvalidOperationException("Unable to create database");
                        }

                        // Since there may be subtle syntax errors in the
                        // migration file that do not cause an outright failure
                        // in execution, we leverage the fact that the version
                        // bump of the DB is at the end of the script. If the
                        // version doesn't match, we'll fail.
                        databaseVersion = Convert.ToInt32(checkIfUpdated.ExecuteScalar());
                        if (!IsCurrentDBVersion(databaseVersion))
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
        public IChangesDatabase ChangesDatabase
        {
            get
            {
                if (this.changesDatabase == null)
                {
                    this.ThrowIfNotReady();
                    this.changesDatabase = PendingChanges.GetPendingChangeDatabase(this.connection, new(Unread: this.UnreadFolderLocalId, Archive: this.ArchiveFolderLocalId));
                }

                return this.changesDatabase;
            }
        }
    }
}
