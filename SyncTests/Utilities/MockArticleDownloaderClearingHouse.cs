using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid;

internal sealed class MockArticleDownloaderEventClearingHouse : IArticleDownloaderEventSink, IArticleDownloaderEventSource
{
    /// <inheritdoc />
    public event EventHandler<int>? DownloadingStarted;

    /// <inheritdoc />
    public event EventHandler<DatabaseArticle>? ArticleStarted;

    /// <inheritdoc />
    public event EventHandler<long>? ImagesStarted;

    /// <inheritdoc />
    public event EventHandler<Uri>? ImageStarted;

    /// <inheritdoc />
    public event EventHandler<Uri>? ImageCompleted;

    /// <inheritdoc />
    public event EventHandler<(Uri Uri, Exception? Error)>? ImageError;

    /// <inheritdoc />
    public event EventHandler<long>? ImagesCompleted;

    /// <inheritdoc />
    public event EventHandler<DatabaseArticle>? ArticleCompleted;

    /// <inheritdoc />
    public event EventHandler<(DatabaseArticle Article, Exception? Error)>? ArticleError;

    /// <inheritdoc />
    public event EventHandler? DownloadingCompleted;

    /// <inheritdoc />
    public void RaiseArticleCompleted(DatabaseArticle article)
    {
        var handler = this.ArticleCompleted;
        handler?.Invoke(this, article);
    }

    /// <inheritdoc />
    public void RaiseArticleStarted(DatabaseArticle article)
    {
        var handler = this.ArticleStarted;
        handler?.Invoke(this, article);
    }

    /// <inheritdoc />
    public void RaiseDownloadingCompleted()
    {
        var handler = this.DownloadingCompleted;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseDownloadingStarted(int numberOfArticlesToDownload)
    {
        var handler = this.DownloadingStarted;
        handler?.Invoke(this, numberOfArticlesToDownload);
    }

    /// <inheritdoc />
    public void RaiseImageCompleted(Uri imageUrl)
    {
        var handler = this.ImageCompleted;
        handler?.Invoke(this, imageUrl);
    }

    /// <inheritdoc />
    public void RaiseImagesCompleted(long articleIdContainingImages)
    {
        var handler = this.ImagesCompleted;
        handler?.Invoke(this, articleIdContainingImages);
    }

    /// <inheritdoc />
    public void RaiseImagesStarted(long articleIdContainingImages)
    {
        var handler = this.ImagesStarted;
        handler?.Invoke(this, articleIdContainingImages);
    }

    /// <inheritdoc />
    public void RaiseImageStarted(Uri imageUrl)
    {
        var handler = this.ImageStarted;
        handler?.Invoke(this, imageUrl);
    }

    /// <inheritdoc />
    public void RaiseImageError(Uri imageUrl, Exception? exception)
    {
        var handler = this.ImageError;
        handler?.Invoke(this, (imageUrl, exception));
    }

    /// <inheritdoc />
    public void RaiseArticleError(DatabaseArticle article, Exception? exception)
    {
        var handler = this.ArticleError;
        handler?.Invoke(this, (article, exception));
    }
}