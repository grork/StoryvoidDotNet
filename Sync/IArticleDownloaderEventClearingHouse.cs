namespace Codevoid.Storyvoid.Sync;

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
    event EventHandler<DatabaseArticle> ArticleStarted;

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
    /// An error occured during downloading of a specific image
    /// </summary>
    event EventHandler<(Uri Uri, Exception? Error)> ImageError;

    /// <summary>
    /// Images for a specific article have completed
    /// </summary>
    event EventHandler<long> ImagesCompleted;

    /// <summary>
    /// Downloading of a specific article is complete
    /// </summary>
    event EventHandler<DatabaseArticle> ArticleCompleted;

    /// <summary>
    /// An error occured during article download
    /// </summary>
    event EventHandler<(DatabaseArticle Article, Exception? Error)> ArticleError;

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
    void RaiseArticleStarted(DatabaseArticle article);

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
    /// When an error occurs while downloading an image
    /// </summary>
    /// <param name="imageUrl">Image URL that the error occured for</param>
    /// <param name="error">Error that occured</param>
    void RaiseImageError(Uri imageUrl, Exception? error);

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
    void RaiseArticleCompleted(DatabaseArticle article);

    /// <summary>
    /// An error occured while downloading a specific article
    /// </summary>
    /// <param name="article">Article for which the error occured</param>
    /// <param name="exception">Error that occured</param>
    void RaiseArticleError(DatabaseArticle article, Exception? error);

    /// <summary>
    /// Raise that downloading of all articles has completed
    /// </summary>
    void RaiseDownloadingCompleted();
}