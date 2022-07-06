namespace Codevoid.Storyvoid;

/// <summary>
/// Events raised from the database for consumers to react to changes in the
/// database *after* they have completed working with the database
/// </summary>
public interface IDatabaseEventSink
{
    public event EventHandler<DatabaseFolder> FolderAdded;
    public event EventHandler<DatabaseFolder> FolderDeleted;
    public event EventHandler<DatabaseFolder> FolderUpdated;

    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)> ArticleAdded;
    public event EventHandler<long> ArticleDeleted;
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)> ArticleMoved;
    public event EventHandler<DatabaseArticle> ArticleUpdated;
}

/// <summary>
/// Interface for components that need to raise the events themselves
/// </summary>
public interface IDatabaseEventSource
{
    public void RaiseFolderAdded(DatabaseFolder added);
    public void RaiseFolderDeleted(DatabaseFolder localFolderId);
    public void RaiseFolderUpdated(DatabaseFolder updated);

    public void RaiseArticleAdded(DatabaseArticle added, long to);
    public void RaiseArticleDeleted(long articleId);
    public void RaiseArticleMoved(DatabaseArticle article, long to);
    public void RaiseArticleUpdated(DatabaseArticle updated);
}

/// <summary>
/// Utility class to hold event source + sink for database changes.
/// </summary>
internal sealed class DatabaseEventClearingHouse : IDatabaseEventSink, IDatabaseEventSource
{
    public event EventHandler<DatabaseFolder>? FolderAdded;
    public event EventHandler<DatabaseFolder>? FolderDeleted;
    public event EventHandler<DatabaseFolder>? FolderUpdated;
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleAdded;
    public event EventHandler<long>? ArticleDeleted;
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleMoved;
    public event EventHandler<DatabaseArticle>? ArticleUpdated;

    public void RaiseFolderAdded(DatabaseFolder added)
    {
        var handler = this.FolderAdded;
        handler?.Invoke(this, added);
    }

    public void RaiseFolderDeleted(DatabaseFolder localFolderId)
    {
        var handler = this.FolderDeleted;
        handler?.Invoke(this, localFolderId);
    }

    public void RaiseFolderUpdated(DatabaseFolder updated)
    {
        var handler = this.FolderUpdated;
        handler?.Invoke(this, updated);
    }

    public void RaiseArticleAdded(DatabaseArticle added, long to)
    {
        var handler = this.ArticleAdded;
        handler?.Invoke(this, (added, to));
    }

    public void RaiseArticleDeleted(long articleId)
    {
        var handler = this.ArticleDeleted;
        handler?.Invoke(this, articleId);
    }

    public void RaiseArticleMoved(DatabaseArticle article, long to)
    {
        var handler = this.ArticleMoved;
        handler?.Invoke(this, (article, to));
    }

    public void RaiseArticleUpdated(DatabaseArticle updated)
    {
        var handler = this.ArticleUpdated;
        handler?.Invoke(this, updated);
    }
}