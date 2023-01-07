using Codevoid.Storyvoid.Sync;
using Microsoft.UI.Dispatching;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Implementation of the article downloader event sink/source that marshalls
/// events onto the dispatcher for easy UI-interactions
/// </summary>
internal class ArticleDownloaderEvents : EventDispatcherBase, IArticleDownloaderEventSink, IArticleDownloaderEventSource
{
    internal ArticleDownloaderEvents(DispatcherQueue queue) : base(queue)
    { }

    /// <inheritdoc/>
    public event EventHandler<int>? DownloadingStarted;

    /// <inheritdoc/>
    public event EventHandler<DatabaseArticle>? ArticleStarted;

    /// <inheritdoc/>
    public event EventHandler<long>? ImagesStarted;

    /// <inheritdoc/>
    public event EventHandler<Uri>? ImageStarted;

    /// <inheritdoc/>
    public event EventHandler<Uri>? ImageCompleted;

    /// <inheritdoc/>
    public event EventHandler<long>? ImagesCompleted;

    /// <inheritdoc/>
    public event EventHandler<DatabaseArticle>? ArticleCompleted;

    /// <inheritdoc/>
    public event EventHandler? DownloadingCompleted;

    /// <inheritdoc/>
    public void RaiseArticleCompleted(DatabaseArticle article)
    {
        var handler = this.ArticleCompleted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, article));
    }

    /// <inheritdoc/>
    public void RaiseArticleStarted(DatabaseArticle article)
    {
        var handler = this.ArticleStarted;
        if(handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, article));
    }

    /// <inheritdoc/>
    public void RaiseDownloadingCompleted()
    {
        var handler = this.DownloadingCompleted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc/>
    public void RaiseDownloadingStarted(int numberOfArticlesToDownload)
    {
        var handler = this.DownloadingStarted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, numberOfArticlesToDownload));
    }

    /// <inheritdoc/>
    public void RaiseImageCompleted(Uri imageUrl)
    {
        var handler = this.ImageCompleted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, imageUrl));
    }

    /// <inheritdoc/>
    public void RaiseImagesCompleted(long articleIdContainingImages)
    {
        var handler = this.ImagesCompleted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, articleIdContainingImages));
    }

    /// <inheritdoc/>
    public void RaiseImagesStarted(long articleIdContainingImages)
    {
        var handler = this.ImagesStarted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, articleIdContainingImages));
    }

    /// <inheritdoc/>
    public void RaiseImageStarted(Uri imageUrl)
    {
        var handler = this.ImageStarted;
        if (handler == null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, imageUrl));
    }
}
