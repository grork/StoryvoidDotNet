namespace Codevoid.Storyvoid;

/// <summary>
/// Record for adding or updating articles to the database
/// </summary>
public record ArticleRecordInformation(
    long id,
    string title,
    Uri url,
    string description,
    float readProgress,
    DateTime readProgressTimestamp,
    string hash,
    bool liked
);

/// <summary>
/// Manage articles in the local Instapaper Database
/// </summary>
public interface IArticleDatabase
{
    /// <summary>
    /// List all articles, across all folders, that are Liked
    /// </summary>
    /// <returns>All articles that are in a Liked state</returns>
    IList<DatabaseArticle> ListLikedArticles();

    /// <summary>
    /// Gets articles for a specific local folder
    /// </summary>
    /// <param name="localId">Local Folder ID to get articles for</param>
    /// <returns>Articles in that folder</returns>
    IList<DatabaseArticle> ListArticlesForLocalFolder(long localId);

    /// <summary>
    /// Lists all the articles in the database, irrespective of their folder
    /// </summary>
    /// <returns>
    /// List of articles in the database, and the folder ID 
    /// </returns>
    IList<(DatabaseArticle Article, long LocalFolderId)> ListAllArticlesInAFolder();

    /// <summary>
    /// List all articles that are not in a folder (e.g. orphaned articles when
    /// a folder is deleted, or they're past the services article limit for a
    /// folder)
    /// </summary>
    /// <returns>List of articles that are not in a folder</returns>
    IList<DatabaseArticle> ListArticlesNotInAFolder();

    /// <summary>
    /// Add a article to the database
    /// </summary>
    /// <param name="data">Article information to add</param>
    /// <returns>Article from the database</returns>
    DatabaseArticle AddArticleToFolder(
        ArticleRecordInformation data,
        long localFolderId);

    /// <summary>
    /// Adds an article, but does not place it in a folder. This means it won't
    /// be returned by any APIs that list articles by folder. There is no
    /// guarantee that the article will remain in the database if it is not
    /// placed in a folder.
    /// 
    /// It can be moved to a folder using <see cref="MoveArticleToFolder(long,long)" />
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    DatabaseArticle AddArticleNoFolder(ArticleRecordInformation data);

    /// <summary>
    /// Updates the specified article with new details, overwriting any
    /// values that are present.
    /// </summary>
    /// <param name="updatedData">Data to update the article with</param>
    /// <returns>article instance with updated values</returns>
    DatabaseArticle UpdateArticle(ArticleRecordInformation updatedData);

    /// <summary>
    /// Gets a article by it's service ID
    /// </summary>
    /// <param name="articleId">ID of the article</param>
    /// <returns>Article if found, null otherwise</returns>
    DatabaseArticle? GetArticleById(long articleId);

    /// <summary>
    /// Like a Article. Will complete even if article is already liked
    /// </summary>
    /// <param name="articleId">Article to Like</param>
    /// <returns>The article after liking. Represents current database state</returns>
    DatabaseArticle LikeArticle(long articleId);

    /// <summary>
    /// Unlike a article. Will complete even if article is already unliked
    /// </summary>
    /// <param name="articleId">Article to Unlike</param>
    /// <returns>The article after unliking. Represents current database state</returns>
    DatabaseArticle UnlikeArticle(long articleId);

    /// <summary>
    /// Update the progress of a specific article, with the supplied
    /// timestamp of the progress update.
    /// </summary>
    /// <param name="readProgress">Progress </param>
    /// <param name="readProgressTimestamp"></param>
    /// <param name="articleId"></param>
    /// <returns></returns>
    DatabaseArticle UpdateReadProgressForArticle(float readProgress, DateTime readProgressTimestamp, long articleId);

    /// <summary>
    /// Moves the specified article to the supplied destination folder
    /// </summary>
    /// <param name="articleId">Article to move</param>
    /// <param name="localFolderId">Folder to move to</param>
    /// <returns></returns>
    void MoveArticleToFolder(long articleId, long localFolderId);

    /// <summary>
    /// Removes an article from any folder it might be in. The folder will no
    /// longer appear in any folder, and will eventually be removed if not
    /// placed into another folder
    /// </summary>
    /// <param name="articleId">Article to remove from a folder</param>
    void RemoveArticleFromAnyFolder(long articleId);

    /// <summary>
    /// Delete a article with the specified ID
    /// </summary>
    /// <param name="articleId">Article to delete</param>
    void DeleteArticle(long articleId);

    /// <summary>
    /// Loads the local only article state for the supplied article ID.
    /// Note, that this will only return data if local only state exists for
    /// the article. Having no data returned does *not* mean that there is
    /// no article with that article ID. Use <see cref="GetArticleById(long)"/>
    /// for that.
    /// </summary>
    /// <param name="articleId">ID of the article to load</param>
    /// <returns>Instance of local only article state, if found</returns>
    DatabaseLocalOnlyArticleState? GetLocalOnlyStateByArticleId(long articleId);

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
    DatabaseLocalOnlyArticleState AddLocalOnlyStateForArticle(DatabaseLocalOnlyArticleState localOnlyArticleState);

    /// <summary>
    /// Deletes the localy only state for the supplied article ID.
    ///
    /// If the ID is not associated with any state, or there is no article
    /// for that ID, no error is raised, and the task completes successfully.
    /// </summary>
    /// <param name="articleId">
    /// ID of the Article for which to delete local only state
    /// </param>
    /// <returns>Task that completes when the data is deleted</returns>
    void DeleteLocalOnlyArticleState(long articleId);

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
    DatabaseLocalOnlyArticleState UpdateLocalOnlyArticleState(DatabaseLocalOnlyArticleState updatedLocalOnlyArticleState);
}

/// <summary>
/// We need to expose events _within_ a transaction, so that errors propagate to
/// cancel the transaction. However, we don't want 'user' behaviour to propagate
/// to cancel  the transactions. So we limit the accessibility of those events
/// </summary>
internal interface IArticleDatabaseWithTransactionEvents : IArticleDatabase
{
    /// <summary>
    /// Raised whenver an article's liked status is changed. Event is raised
    /// with the *new* status.
    /// </summary>
    event WithinTransactionEventHandler<IArticleDatabase, DatabaseArticle> ArticleLikeStatusChangedWithinTransaction;

    /// <summary>
    /// Raised when an article has been completely deleted from the database.
    /// Because the article is being deleted, only the ID is provided.
    /// </summary>
    event WithinTransactionEventHandler<IArticleDatabase, long> ArticleDeletedWithinTransaction;

    /// <summary>
    /// Raised when an article is moved between folders. When raised, the
    /// article being moved is supplied, along with the local ID of the folder
    /// its being moved to.
    /// </summary>
    event WithinTransactionEventHandler<IArticleDatabase, (DatabaseArticle Article, long DestinationLocalFolderId)> ArticleMovedToFolderWithinTransaction;
}