using Microsoft.UI.Dispatching;

namespace Codevoid.Storyvoid;

/// <summary>
/// Handles database events being raised, and ensures that listeners are handled
/// on the dispatcher provided during construction. If there are no listeners
/// for an event, that event is not raised.
/// 
/// This is intended to help with events that impact the *UI*, and thus need to
/// be executed on the event 
/// </summary>
internal class DispatcherDatabaseEvents : IDatabaseEventSink, IDatabaseEventSource
{
    private readonly DispatcherQueue queue;

    /// <summary>
    /// Instantiate w/ the supplied dispatcher
    /// </summary>
    /// <param name="targetDispatcherQueue"></param>
    internal DispatcherDatabaseEvents(DispatcherQueue targetDispatcherQueue)
    {
        this.queue = targetDispatcherQueue;
    }

    /// <summary>
    /// Invokes the supplied operation on the dispatcher. If we're already on
    /// the dispatcher, we will execute it directly. Otherwise we'll queue it on
    /// our dispatcher queue for later execution.
    /// </summary>
    /// <param name="operation">Encapsulated operation to perform</param>
    private void PerformOnDispatcher(DispatcherQueueHandler operation)
    {
        if (this.queue.HasThreadAccess)
        {
            // If we're already on the right thread, we can just invoke the
            // operation directly.
            operation();
            return;
        }

        this.queue.TryEnqueue(operation);
    }

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
    public event EventHandler<(DatabaseArticle Article, long DestinationLocalFolderId)>? ArticleMoved;

    /// <inheritdoc />
    public event EventHandler<DatabaseArticle>? ArticleUpdated;

    /// <inheritdoc />
    public void RaiseArticleAdded(DatabaseArticle added, long toLocalId)
    {
        var handler = this.ArticleAdded;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, (added, toLocalId)));
    }

    /// <inheritdoc />
    public void RaiseArticleDeleted(long articleId)
    {
        var handler = this.ArticleDeleted;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, articleId));
    }

    /// <inheritdoc />
    public void RaiseArticleMoved(DatabaseArticle article, long toLocalId)
    {
        var handler = this.ArticleMoved;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, (article, toLocalId)));
    }

    /// <inheritdoc />
    public void RaiseArticleUpdated(DatabaseArticle updated)
    {
        var handler = this.ArticleUpdated;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, updated));
    }

    /// <inheritdoc />
    public void RaiseFolderAdded(DatabaseFolder added)
    {
        var handler = this.FolderAdded;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, added));
    }

    /// <inheritdoc />
    public void RaiseFolderDeleted(DatabaseFolder deleted)
    {
        var handler = this.FolderDeleted;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, deleted));
    }

    /// <inheritdoc />
    public void RaiseFolderUpdated(DatabaseFolder updated)
    {
        var handler = this.FolderUpdated;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, updated));
    }
}
