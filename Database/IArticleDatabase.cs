using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    /// Record for adding or updating bookmarks to the database
    /// </summary>
    public record BookmarkRecordInformation(long id, string title, Uri url, string description, float readProgress, DateTime readProgressTimestamp, string hash, bool liked);

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
        Task<IList<DatabaseFolder>> ListAllFoldersAsync();

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
        /// <param name="shouldSync">Should the folder by synced</param>
        /// <returns>The folder after being added to the database</returns>
        Task<DatabaseFolder> AddKnownFolderAsync(string title, long serviceId, long position, bool shouldSync);

        /// <summary>
        /// Updates the data of a folder with the supplied Local ID. All fields
        /// must be supplied.
        /// </summary>
        /// <param name="localId">Item to update</param>
        /// <param name="serviceId">Service ID to set</param>
        /// <param name="title">Title to set</param>
        /// <param name="position">Position to set</param>
        /// <param name="shouldSync">Should be synced</param>
        /// <returns>Updated folder</returns>
        Task<DatabaseFolder> UpdateFolderAsync(long localId, long? serviceId, string title, long position, bool shouldSync);

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
        Task<IList<DatabaseBookmark>> ListLikedBookmarksAsync();

        /// <summary>
        /// Gets Bookmarks for a specific local folder
        /// </summary>
        /// <param name="localId">Local Folder ID to get bookmarks for</param>
        /// <returns>Bookmarks in that folder</returns>
        Task<IList<DatabaseBookmark>> ListBookmarksForLocalFolderAsync(long localId);

        /// <summary>
        /// Add a bookmark to the database
        /// </summary>
        /// <param name="data">Bookmark information to add</param>
        /// <returns>Bookmark from the database</returns>
        Task<DatabaseBookmark> AddBookmarkToFolderAsync(
            BookmarkRecordInformation data,
            long localFolderId);

        /// <summary>
        /// Updates the specified bookmark with new details, overwriting any
        /// values that are present.
        /// </summary>
        /// <param name="updatedData">Data to update the bookmark with</param>
        /// <returns>Bookmark instance with updated values</returns>
        Task<DatabaseBookmark> UpdateBookmarkAsync(BookmarkRecordInformation updatedData);

        /// <summary>
        /// Gets a bookmark by it's service ID
        /// </summary>
        /// <param name="bookmarkId">ID of the bookmark</param>
        /// <returns>Bookmark if found, null otherwise</returns>
        Task<DatabaseBookmark?> GetBookmarkByIdAsync(long bookmarkId);

        /// <summary>
        /// Like a bookmark. Will complete even if bookmark is already liked
        /// </summary>
        /// <param name="bookmarkId">Bookmark to Like</param>
        /// <returns>The Bookmark after liking. Represents current database state</returns>
        Task<DatabaseBookmark> LikeBookmarkAsync(long bookmarkId);

        /// <summary>
        /// Unlike a bookmark. Will complete even if bookmark is already unliked
        /// </summary>
        /// <param name="bookmarkId">Bookmark to Unlike</param>
        /// <returns>The Bookmark after unliking. Represents current database state</returns>
        Task<DatabaseBookmark> UnlikeBookmarkAsync(long bookmarkId);

        /// <summary>
        /// Update the progress of a specific bookmark, with the supplied
        /// timestamp of the progress update.
        /// </summary>
        /// <param name="readProgress">Progress </param>
        /// <param name="readProgressTimestamp"></param>
        /// <param name="bookmarkId"></param>
        /// <returns></returns>
        Task<DatabaseBookmark> UpdateReadProgressForBookmarkAsync(float readProgress, DateTime readProgressTimestamp, long bookmarkId);

        /// <summary>
        /// Moves the specified bookmark to the supplied destination folder
        /// </summary>
        /// <param name="bookmarkId">Bookmark to move</param>
        /// <param name="localFolderId">Folder to move to</param>
        /// <returns></returns>
        Task MoveBookmarkToFolderAsync(long bookmarkId, long localFolderId);

        /// <summary>
        /// Delete a bookmark with the specified ID
        /// </summary>
        /// <param name="bookmarkId">Bookmark to delete</param>
        Task DeleteBookmarkAsync(long bookmarkId);

        /// <summary>
        /// Loads the local only bookmark state for the supplied bookmark ID.
        /// Note, that this will only return data if local only state exists for
        /// the bookmark. Having no data returned does *not* mean that there is
        /// no bookmark with that bookmark ID. Use <see cref="GetBookmarkByIdAsync(long)"/>
        /// for that.
        /// </summary>
        /// <param name="bookmarkId">ID of the bookmark to load</param>
        /// <returns>Instance of local only bookmark state, if found</returns>
        Task<DatabaseLocalOnlyBookmarkState?> GetLocalOnlyStateByBookmarkIdAsync(long bookmarkId);

        /// <summary>
        /// Add a bookmark with the supplied data. This will succeed only if a
        /// bookmark with the supplied ID exists, and no other local state
        /// has been previously added
        /// </summary>
        /// <param name="localOnlyBookmarkState">Local only state to add</param>
        /// <returns>Local only state as return from storage</returns>
        /// <exception cref="BookmarkNotFoundException">
        /// If no bookmark exists with the supplied bookmark ID
        /// </exception>
        /// <exception cref="LocalOnlyStateExistsException">
        /// When local only state already exists with the supplied bookmark ID
        /// </exception>
        Task<DatabaseLocalOnlyBookmarkState> AddLocalOnlyStateForBookmarkAsync(DatabaseLocalOnlyBookmarkState localOnlyBookmarkState);
    }
}