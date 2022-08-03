namespace Codevoid.Storyvoid.Sync;

public record DownloadArticleArgs(long ArticleId, string Title);

/// <summary>
/// Event sink for article downloading, allowing components to observe when
/// downloads start, the constiuent phases, and completion.
/// 
/// It's important to note that it is up to the listener to ensure they handle
/// the event on the correct thread.
/// </summary>
public interface IArticleDownloaderEventSink
{
    /// <summary>
    /// Downloading of articles has begun. When raised, the number of articles
    /// to be downloaded is included.
    /// </summary>
    event EventHandler<int> DownloadingStarted;

    /// <summary>
    /// A specific article download has begin, including it's images
    /// </summary>
    event EventHandler<DownloadArticleArgs> ArticleStarted;

    /// <summary>
    /// Images for a specific article have begun
    /// </summary>
    event EventHandler<long> ImagesStarted;

    /// <summary>
    /// Downloading of a specific image has begun
    /// </summary>
    event EventHandler<Uri> ImageStarted;

    /// <summary>
    /// Downloading of a specific image has completed
    /// </summary>
    event EventHandler<Uri> ImageCompleted;

    /// <summary>
    /// Images for a specific article have completed
    /// </summary>
    event EventHandler<long> ImagesCompleted;

    /// <summary>
    /// Downloading of a specific article is complete
    /// </summary>
    event EventHandler<DownloadArticleArgs> ArticleCompleted;

    /// <summary>
    /// Downloading of articles is complete.
    /// </summary>
    event EventHandler DownloadingCompleted;
}

/// <summary>
/// Event source to raise events related to article downloading
/// </summary>
public interface IArticleDownloaderEventSource
{
    /// <summary>
    /// Raises that a number of articles are beginning a download
    /// </summary>
    /// <param name="numberOfArticlesToDownload">
    /// Number of articles that will be downloaded
    /// </param>
    void RaiseDownloadingStarted(int numberOfArticlesToDownload);

    /// <summary>
    /// An article has started downloading
    /// </summary>
    /// <param name="articleInformation">
    /// Information about the article being downloaded
    /// </param>
    void RaiseArticleStarted(DownloadArticleArgs articleInformation);

    /// <summary>
    /// Images in a specific article have started downloading
    /// </summary>
    /// <param name="ArticContainingImages">
    /// Article ID for which images are being downloaded
    /// </param>
    void RaiseImagesStarted(long articleIdContainingImages);

    /// <summary>
    /// An image is being downloaded
    /// </summary>
    /// <param name="imageUrl">Image URL being downloaded</param>
    void RaiseImageStarted(Uri imageUrl);

    /// <summary>
    /// An image has completed downloading
    /// </summary>
    /// <param name="imageUrl">Image URL that has completed</param>
    void RaiseImageCompleted(Uri imageUrl);

    /// <summary>
    /// Images for a specific article have completed downloading
    /// </summary>
    /// <param name="articleIdContainingImages">
    /// Article ID for which images are complete
    /// </param>
    void RaiseImagesCompleted(long articleIdContainingImages);

    /// <summary>
    /// An article, and it's images, have completed downloading.
    /// </summary>
    /// <param name="articleInformation">
    /// Information about the article that has completed
    /// </param>
    void RaiseArticleCompleted(DownloadArticleArgs articleInformation);

    /// <summary>
    /// Raise that downloading of all articles has completed
    /// </summary>
    void RaiseDownloadingCompleted();
}