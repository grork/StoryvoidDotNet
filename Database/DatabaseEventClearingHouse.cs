namespace Codevoid.Storyvoid;

/// <summary>
/// Events raised from the database for consumers to react to changes in the
/// database *after* they have completed working with the database
/// </summary>
public interface IDatabaseEventSource
{
    public event EventHandler<DatabaseFolder> FolderAdded;
    public event EventHandler<DatabaseFolder> FolderDeleted;
    public event EventHandler<DatabaseFolder> FolderUpdated;

    public event EventHandler<DatabaseArticle> ArticleAdded;
    public event EventHandler<DatabaseArticle> ArticleDeleted;
    public event EventHandler<(DatabaseArticle Article, long To)> ArticleMoved;
    public event EventHandler<DatabaseArticle> ArticleUpdated;
}

/// <summary>
/// Interface for components that need to raise the events themselves
/// </summary>
public interface IDatabaseEventSink
{
    public void RaiseFolderAdded(DatabaseFolder added);
    public void RaiseFolderDeleted(DatabaseFolder deleted);
    public void RaiseFolderUpdated(DatabaseFolder updated);

    public void RaiseArticleAdded(DatabaseArticle added);
    public void RaiseArticleDeleted(DatabaseArticle deleted);
    public void RaiseArticleMoved(DatabaseArticle article, long to);
    public void RaiseArticleUpdated(DatabaseArticle updated);
}

/// <summary>
/// Utility class to hold event source + sink for database changes.
/// </summary>
internal sealed class DatabaseEventClearingHouse : IDatabaseEventSource, IDatabaseEventSink
{
    public event EventHandler<DatabaseFolder>? FolderAdded;
    public event EventHandler<DatabaseFolder>? FolderDeleted;
    public event EventHandler<DatabaseFolder>? FolderUpdated;
    public event EventHandler<DatabaseArticle>? ArticleAdded;
    public event EventHandler<DatabaseArticle>? ArticleDeleted;
    public event EventHandler<(DatabaseArticle Article, long To)>? ArticleMoved;
    public event EventHandler<DatabaseArticle>? ArticleUpdated;

    public void RaiseFolderAdded(DatabaseFolder added)
    {
        var handler = this.FolderAdded;
        handler?.Invoke(this, added);
    }

    public void RaiseFolderDeleted(DatabaseFolder deleted)
    {
        var handler = this.FolderDeleted;
        handler?.Invoke(this, deleted);
    }

    public void RaiseFolderUpdated(DatabaseFolder updated)
    {
        var handler = this.FolderUpdated;
        handler?.Invoke(this, updated);
    }

    public void RaiseArticleAdded(DatabaseArticle added)
    {
        var handler = this.ArticleAdded;
        handler?.Invoke(this, added);
    }

    public void RaiseArticleDeleted(DatabaseArticle deleted)
    {
        var handler = this.ArticleDeleted;
        handler?.Invoke(this, deleted);
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