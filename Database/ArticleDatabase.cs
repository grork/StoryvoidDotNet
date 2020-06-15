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

    /// <summary>
    /// Database store for Bookmarks &amp; Folders from the Instapaper Service
    /// </summary>
    public interface IArticleDatabase : IDisposable
    {
        /// <summary>
        /// The database ID of the unread folder
        /// </summary>
        long UnreadFolderLocalId { get; }

        /// <summary>
        /// The database ID of the archive folder
        /// </summary>
        long ArchiveFolderLocalId { get; }

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

        /// <summary>
        /// Delete the specified folder. Any bookmarks in this folder will be
        /// orphaned until they're reconciled against the server, or othewise
        /// removed.
        /// </summary>
        /// <param name="localFolderId">Folder to delete</param>
        Task DeleteFolderAsync(long localFolderId);

        /// <summary>
        /// List all bookmarks, across all folders, that are Liked
        /// </summary>
        /// <returns>All bookmarks that are in a Liked state</returns>
        Task<IList<DatabaseBookmark>> GetLikedBookmarks();

        /// <summary>
        /// Gets Bookmarks for a specific local folder
        /// </summary>
        /// <param name="localId">Local Folder ID to get bookmarks for</param>
        /// <returns>Bookmarks in that folder</returns>
        Task<IList<DatabaseBookmark>> GetBookmarks(long localId);

        /// <summary>
        /// Add a bookmark to the database
        /// </summary>
        /// <param name="id">ID of the bookmark on the service</param>
        /// <param name="title">Title of the bookmark</param>
        /// <param name="url">URL the bookmark is for</param>
        /// <param name="description">Description of the bookmark</param>
        /// <param name="progress">Current read progress</param>
        /// <param name="progressTimestamp">Last time progress was changed</param>
        /// <param name="hash">Service-sourced hash of the bookmark state</param>
        /// <param name="liked">Liked status of the bookmark</param>
        /// <param name="localFolderId">Folder to place this bookmark into</param>
        /// <returns>Bookmark from the database</returns>
        Task<DatabaseBookmark> AddBookmark(
            (int id, string title, Uri url, string description, float progress, DateTime progressTimestamp, string hash, bool liked) data,
            long localFolderId);

        /// <summary>
        /// Gets a bookmark by it's service ID
        /// </summary>
        /// <param name="id">ID of the bookmark</param>
        /// <returns>Bookmark if found, null otherwise</returns>
        Task<DatabaseBookmark?> GetBookmarkById(long id);

        /// <summary>
        /// Like a bookmark. Will complete even if bookmark is already liked
        /// </summary>
        /// <param name="id">Bookmark to Like</param>
        /// <returns>The Bookmark after liking. Represents current database state</returns>
        Task<DatabaseBookmark> LikeBookmark(long id);

        /// <summary>
        /// Unlike a bookmark. Will complete even if bookmark is already unliked
        /// </summary>
        /// <param name="id">Bookmark to Unlike</param>
        /// <returns>The Bookmark after unliking. Represents current database state</returns>
        Task<DatabaseBookmark> UnlikeBookmark(long id);

        /// <summary>
        /// Update the progress of a specific bookmark, with the supplied
        /// timestamp of the progress update.
        /// </summary>
        /// <param name="progress">Progress </param>
        /// <param name="progressTimestamp"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<DatabaseBookmark> UpdateProgressForBookmark(float progress, DateTime progressTimestamp, long id);

        /// <summary>
        /// Moves the specified bookmark to the supplied destination folder
        /// </summary>
        /// <param name="bookmarkId">Bookmark to move</param>
        /// <param name="localFolderId">Folder to move to</param>
        /// <returns></returns>
        Task MoveBookmarkToFolder(long bookmarkId, long localFolderId);

        /// <summary>
        /// Delete a bookmark with the specified ID
        /// </summary>
        /// <param name="bookmarkId">Bookmark to delete</param>
        Task DeleteBookmark(long bookmarkId);
    }

    public sealed partial class ArticleDatabase : IArticleDatabase, IDisposable
    {
        // To help diagnose calls that skipped initialization
        private int initialized = 0;
        private const int CURRENT_DB_VERSION = 1;

        public long UnreadFolderLocalId { get; private set; }
        public long ArchiveFolderLocalId { get; private set; }

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
    }
}
