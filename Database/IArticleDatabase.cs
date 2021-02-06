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
        /// Default folder on the service, where new articles are placed by
        /// default.
        /// </summary>
        public const long Unread = -1;

        /// <summary>
        /// Folder for articles that have been archived by the user.
        /// </summary>
        public const long Archive = -2;
    }

    /// <summary>
    /// Record for adding or updating articles to the database
    /// </summary>
    public record ArticleRecordInformation(long id, string title, Uri url, string description, float readProgress, DateTime readProgressTimestamp, string hash, bool liked);

    /// <summary>
    /// Database store for Articles &amp; Folders from the Instapaper Service
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
        /// Delete the specified folder. Any articles in this folder will be
        /// orphaned until they're reconciled against the server, or othewise
        /// removed.
        /// </summary>
        /// <param name="localFolderId">Folder to delete</param>
        Task DeleteFolderAsync(long localFolderId);

        /// <summary>
        /// List all articles, across all folders, that are Liked
        /// </summary>
        /// <returns>All articles that are in a Liked state</returns>
        Task<IList<DatabaseArticle>> ListLikedArticleAsync();

        /// <summary>
        /// Gets articles for a specific local folder
        /// </summary>
        /// <param name="localId">Local Folder ID to get articles for</param>
        /// <returns>Articles in that folder</returns>
        Task<IList<DatabaseArticle>> ListArticlesForLocalFolderAsync(long localId);

        /// <summary>
        /// Add a article to the database
        /// </summary>
        /// <param name="data">Article information to add</param>
        /// <returns>Article from the database</returns>
        Task<DatabaseArticle> AddArticleToFolderAsync(
            ArticleRecordInformation data,
            long localFolderId);

        /// <summary>
        /// Updates the specified article with new details, overwriting any
        /// values that are present.
        /// </summary>
        /// <param name="updatedData">Data to update the article with</param>
        /// <returns>article instance with updated values</returns>
        Task<DatabaseArticle> UpdateArticleAsync(ArticleRecordInformation updatedData);

        /// <summary>
        /// Gets a article by it's service ID
        /// </summary>
        /// <param name="articleId">ID of the article</param>
        /// <returns>Article if found, null otherwise</returns>
        Task<DatabaseArticle?> GetArticleByIdAsync(long articleId);

        /// <summary>
        /// Like a Article. Will complete even if article is already liked
        /// </summary>
        /// <param name="articleId">Article to Like</param>
        /// <returns>The article after liking. Represents current database state</returns>
        Task<DatabaseArticle> LikeArticleAsync(long articleId);

        /// <summary>
        /// Unlike a article. Will complete even if article is already unliked
        /// </summary>
        /// <param name="articleId">Article to Unlike</param>
        /// <returns>The article after unliking. Represents current database state</returns>
        Task<DatabaseArticle> UnlikeArticleAsync(long articleId);

        /// <summary>
        /// Update the progress of a specific article, with the supplied
        /// timestamp of the progress update.
        /// </summary>
        /// <param name="readProgress">Progress </param>
        /// <param name="readProgressTimestamp"></param>
        /// <param name="articleId"></param>
        /// <returns></returns>
        Task<DatabaseArticle> UpdateReadProgressForArticleAsync(float readProgress, DateTime readProgressTimestamp, long articleId);

        /// <summary>
        /// Moves the specified article to the supplied destination folder
        /// </summary>
        /// <param name="articleId">Article to move</param>
        /// <param name="localFolderId">Folder to move to</param>
        /// <returns></returns>
        Task MoveArticleToFolderAsync(long articleId, long localFolderId);

        /// <summary>
        /// Delete a article with the specified ID
        /// </summary>
        /// <param name="articleId">Article to delete</param>
        Task DeleteArticleAsync(long articleId);

        /// <summary>
        /// Loads the local only article state for the supplied article ID.
        /// Note, that this will only return data if local only state exists for
        /// the article. Having no data returned does *not* mean that there is
        /// no article with that article ID. Use <see cref="GetArticleByIdAsync(long)"/>
        /// for that.
        /// </summary>
        /// <param name="articleId">ID of the article to load</param>
        /// <returns>Instance of local only article state, if found</returns>
        Task<DatabaseLocalOnlyArticleState?> GetLocalOnlyStateByArticleIdAsync(long articleId);

        /// <summary>
        /// Add a article with the supplied data. This will succeed only if a
        /// article with the supplied ID exists, and no other local state
        /// has been previously added
        /// </summary>
        /// <param name="localOnlyArticleState">Local only state to add</param>
        /// <returns>Local only state as return from storage</returns>
        /// <exception cref="ArticleNotFoundException">
        /// If no article exists with the supplied article ID
        /// </exception>
        /// <exception cref="LocalOnlyStateExistsException">
        /// When local only state already exists with the supplied article ID
        /// </exception>
        Task<DatabaseLocalOnlyArticleState> AddLocalOnlyStateForArticleAsync(DatabaseLocalOnlyArticleState localOnlyArticleState);
    }
}