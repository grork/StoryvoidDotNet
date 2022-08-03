using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid;

internal sealed class MockArticleDownloaderEventClearingHouse : IArticleDownloaderEventSink, IArticleDownloaderEventSource
{
    /// <inheritdoc />
    public event EventHandler<int>? DownloadingStarted;

    /// <inheritdoc />
    public event EventHandler<DownloadArticleArgs>? ArticleStarted;
    
    /// <inheritdoc />
    public event EventHandler<long>? ImagesStarted;
    
    /// <inheritdoc />
    public event EventHandler<Uri>? ImageStarted;
    
    /// <inheritdoc />
    public event EventHandler<Uri>? ImageCompleted;
    
    /// <inheritdoc />
    public event EventHandler<long>? ImagesCompleted;
    
    /// <inheritdoc />
    public event EventHandler<DownloadArticleArgs>? ArticleCompleted;
    
    /// <inheritdoc />
    public event EventHandler? DownloadingCompleted;

    /// <inheritdoc />
    public void RaiseArticleCompleted(DownloadArticleArgs articleInformation)
    {
        var handler = this.ArticleCompleted;
        handler?.Invoke(this, articleInformation);
    }

    /// <inheritdoc />
    public void RaiseArticleStarted(DownloadArticleArgs articleInformation)
    {
        var handler = this.ArticleStarted;
        handler?.Invoke(this, articleInformation);
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
}