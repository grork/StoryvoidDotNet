using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Record for adding or updating articles to the database
    /// </summary>
    public record ArticleRecordInformation(long id, string title, Uri url, string description, float readProgress, DateTime readProgressTimestamp, string hash, bool liked);

    /// <summary>
    /// Manage articles in the local Instapaper Database
    /// </summary>
    public interface IArticleDatabase
    {
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

        /// <summary>
        /// Deletes the localy only state for the supplied article ID.
        ///
        /// If the ID is not associated with any state, or there is no article
        /// for that ID, no error is raised, and the task completes successfully.
        /// </summary>
        /// <param name="articleId">
        /// ID of the Article for which to delete local only state
        /// </param>
        /// <returns>Task that completes when the data is removed</returns>
        Task DeleteLocalOnlyArticleStateAsync(long articleId);

        /// <summary>
        /// Updates the information associated with an existing Local Only
        /// Artical State, replacing all information.
        ///
        /// If there is no existing information, an <see cref="LocalOnlyStateNotFoundException"/>
        /// exception will be thrown.
        /// </summary>
        /// <param name="updatedLocalOnlyArticleState">
        /// The desired data to be updated for this article
        /// </param>
        /// <returns>The updated details</returns>
        Task<DatabaseLocalOnlyArticleState> UpdateLocalOnlyArticleStateAsync(DatabaseLocalOnlyArticleState updatedLocalOnlyArticleState);
    }
}