namespace Codevoid.Storyvoid;

/// <summary>
/// Events raised from the database for consumers to react to changes in the
/// database *after* they have committed data to the database.
/// 
/// It's important to note that it is up to the listener to ensure they handle
/// the event on the correct thread.
/// </summary>
public interface IDatabaseEventSink
{
    /// <summary>
    /// Raised when a folder is added to the database -- either because of user
    /// action, or because of a system operation. Includes the complete folder
    /// information at the time of the addition.
    /// </summary>
    event EventHandler<DatabaseFolder> FolderAdded;

    /// <summary>
    /// Raised when a folder is removed from the database -- either because of
    /// user action, or because of a system operation. Includes the complete
    /// folder information at the time of the removal.
    /// </summary>
    event EventHandler<DatabaseFolder> FolderDeleted;

    /// <summary>
    /// Raised when a folder is update -- either because of user action, or
    /// because of a system operation. Includes the the new folder information,
    /// after the update is complete.
    /// </summary>
    event EventHandler<DatabaseFolder> FolderUpdated;

    /// <summary>
    /// Raised when an article is added to the database -- either because of
    /// user action, or because of a system operation. Includes the complete
    /// article information at the time of the addition.
    /// </summary>
    event EventHandler<(DatabaseArticle Article, long LocalFolderId)> ArticleAdded;

    /// <summary>
    /// Raised when an article is removed from the database or a folder --
    /// either because of user action, or because of a system operation.
    /// Includes only the article ID that was deleted, and no other information.
    /// </summary>
    event EventHandler<long> ArticleDeleted;

    /// <summary>
    /// Raised when an article is moved to a new folder -- either because of
    /// user action, or because of a system operation. Includes the complete
    /// article information, and the destination folder. The source folder is
    /// not included.
    /// </summary>
    event EventHandler<(DatabaseArticle Article, long LocalFolderId)> ArticleMoved;

    /// <summary>
    /// Raised when an article is updated -- either because of user action, or
    /// because of a system operation. Includes the complete article information
    /// at the time of the update.
    /// </summary>
    event EventHandler<DatabaseArticle> ArticleUpdated;
}

/// <summary>
/// Interface for components that need to raise the events on the clearing house
/// </summary>
internal interface IDatabaseEventSource
{
    /// <summary>
    /// Raise a folder added event
    /// </summary>
    /// <param name="added">The folder that was added</param>
    void RaiseFolderAdded(DatabaseFolder added);

    /// <summary>
    /// Raises a folder delete event
    /// </summary>
    /// <param name="deleted">Folder that was deleted</param>
    void RaiseFolderDeleted(DatabaseFolder deleted);

    /// <summary>
    /// Raise a folder updated event
    /// </summary>
    /// <param name="updated">Updated folder information</param>
    void RaiseFolderUpdated(DatabaseFolder updated);

    /// <summary>
    /// Raise an article added event
    /// </summary>
    /// <param name="added">Article that was added</param>
    /// <param name="toLocalId">Local ID of the folder it was added to</param>
    void RaiseArticleAdded(DatabaseArticle added, long toLocalId);

    /// <summary>
    /// Raise an article deleted event
    /// </summary>
    /// <param name="articleId">ID of the article that was deleted</param>
    void RaiseArticleDeleted(long articleId);

    /// <summary>
    /// Raise an article moved event
    /// </summary>
    /// <param name="article">Article that was moved</param>
    /// <param name="toLocalId">Local ID of the folder it was moved to</param>
    void RaiseArticleMoved(DatabaseArticle article, long toLocalId);

    /// <summary>
    /// Raise an article updated event
    /// </summary>
    /// <param name="updated">Article that was udpated</param>
    void RaiseArticleUpdated(DatabaseArticle updated);
}

/// <summary>
/// Utility class to hold event source + sink for database changes.
/// </summary>
internal sealed class DatabaseEventClearingHouse : IDatabaseEventSink, IDatabaseEventSource
{
    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderAdded;
    
    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderDeleted;
    
    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderUpdated;
    
    /// <inheritdoc />
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleAdded;
    
    /// <inheritdoc />
    public event EventHandler<long>? ArticleDeleted;
    
    /// <inheritdoc />
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleMoved;
    
    /// <inheritdoc />
    public event EventHandler<DatabaseArticle>? ArticleUpdated;
    
    /// <inheritdoc />
    public void RaiseFolderAdded(DatabaseFolder added)
    {
        var handler = this.FolderAdded;
        handler?.Invoke(this, added);
    }

    /// <inheritdoc />
    public void RaiseFolderDeleted(DatabaseFolder deleted)
    {
        var handler = this.FolderDeleted;
        handler?.Invoke(this, deleted);
    }

    /// <inheritdoc />
    public void RaiseFolderUpdated(DatabaseFolder updated)
    {
        var handler = this.FolderUpdated;
        handler?.Invoke(this, updated);
    }

    /// <inheritdoc />
    public void RaiseArticleAdded(DatabaseArticle added, long toLocalId)
    {
        var handler = this.ArticleAdded;
        handler?.Invoke(this, (added, toLocalId));
    }

    /// <inheritdoc />
    public void RaiseArticleDeleted(long articleId)
    {
        var handler = this.ArticleDeleted;
        handler?.Invoke(this, articleId);
    }

    /// <inheritdoc />
    public void RaiseArticleMoved(DatabaseArticle article, long toLocalId)
    {
        var handler = this.ArticleMoved;
        handler?.Invoke(this, (article, toLocalId));
    }

    /// <inheritdoc />
    public void RaiseArticleUpdated(DatabaseArticle updated)
    {
        var handler = this.ArticleUpdated;
        handler?.Invoke(this, updated);
    }
}