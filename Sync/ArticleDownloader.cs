using System.Text;
using System.Diagnostics;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharpConfiguration = AngleSharp.Configuration;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using SixLabors.ImageSharp;

namespace Codevoid.Storyvoid.Sync;

/// <summary>
/// Helper to separate an enumeration into fixed-size chunks for batching.
///
/// While .net does contain a chunk function, it's only in newer versions (.net
/// core 6+), and we are targeting .net standard 2.0. If that changes, we should
/// switch to that. See here for info:
/// https://docs.microsoft.com/en-us/dotnet/api/system.linq.enumerable.chunk?view=net-6.0
/// </summary>
internal static class ChunkinatorExtension
{
    /// <summary>
    /// For the supplied source, break the contents into chunks no more than
    /// chunkSize'd batches. If there are not enough items to fill a chunk, then
    /// a chunk smaller than that will be returned/
    /// </summary>
    /// <typeparam name="T">Type of items being chunked</typeparam>
    /// <param name="chunkSize">Size of chunks. Must be greater than 0</param>
    /// <returns>Enumerable that itself returnes Enumeables of
    /// chunkSize</returns>
    /// <exception cref="ArgumentOutOfRangeException">If chunkSize is less than
    /// 1</exception>
    internal static IEnumerable<IEnumerable<T>> Chunkify<T>(this IEnumerable<T> instance, int chunkSize)
    {
        if (chunkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be more than 0");
        }
        return ChunkEnumeration(instance, chunkSize);
    }

    /// <summary>
    /// Simple helper so that the yeilding works, and the exception is thrown
    /// before evaluation starts rather than on first element enumeration.
    /// </summary>
    private static IEnumerable<IEnumerable<T>> ChunkEnumeration<T>(IEnumerable<T> instance, int chunkSize)
    {
        var enumerator = instance.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var chunk = new List<T>(chunkSize);
            chunk.Add(enumerator.Current);

            for (var index = 1; (index < chunkSize) && enumerator.MoveNext(); index += 1)
            {
                chunk.Add(enumerator.Current);
            }

            yield return chunk;
        }
    }
}

/// <summary>
/// Downloads Articles (and images) from the service, and updates the local
/// state to reflect download success and local file paths
/// </summary>
public class ArticleDownloader : IDisposable
{
    private record FirstImageInformaton(Uri FirstLocalImage, Uri FirstRemoteImage);
    private record ProcessedArticleInformation(string Body, string ExtractedDescription, FirstImageInformaton? FirstImage);

    private const int MINIMUM_IMAGE_DIMENSION = 150;
    private const string SVG_CONTENT_TYPE = "image/svg+xml";

    private static Lazy<IConfiguration> lazyParserConfiguration = new Lazy<IConfiguration>(() =>
    {
        // Remove 'dangerous' aspects of AngleSharps API so bad things can't
        // happen
        var configuration = AngleSharpConfiguration.Default.WithCss(); // Lets us use GetInnerText()
        configuration = configuration.Without<AngleSharp.Dom.Events.IEventFactory>();
        configuration = configuration.Without<AngleSharp.Dom.IAttributeObserver>();
        configuration = configuration.Without<AngleSharp.Browser.INavigationHandler>();

        return configuration;
    }, true);

    internal static IConfiguration ParserConfiguration => lazyParserConfiguration.Value;

    private Lazy<HttpClient> ImageClient;
    private string workingRoot;
    private IArticleDatabase articleDatabase;
    private IBookmarksClient bookmarksClient;
    private IArticleDownloaderEventSource? eventSource;
    private IDictionary<long, Task<DatabaseLocalOnlyArticleState?>> inProgressDownloads = new Dictionary<long, Task<DatabaseLocalOnlyArticleState?>>();

    /// <summary>
    /// Create a downloader instance
    /// </summary>
    /// <param name="workingRoot">
    /// File system path that we will download articles to. It is assumed that
    /// this exists, and is accessible. No checks will be performed before being
    /// written to.
    /// </param>
    /// <param name="articleDatabase">Database to use for article information</param>
    /// <param name="bookmarksClient">Bookmark Service client</param>
    /// <param name="clientInformation">Client information to use for the user agent when requesting images</param>
    public ArticleDownloader(string workingRoot,
                             IArticleDatabase articleDatabase,
                             IBookmarksClient bookmarksClient,
                             ClientInformation clientInformation,
                             IArticleDownloaderEventSource? eventSource = null)
        : this(null, workingRoot, articleDatabase, bookmarksClient, clientInformation, eventSource)
    { }

    /// <summary>
    /// Internal to faciliate testing by mocking the request handler
    /// </summary>
    internal ArticleDownloader(HttpMessageHandler? handler,
                               string workingRoot,
                               IArticleDatabase articleDatabase,
                               IBookmarksClient bookmarksClient,
                               ClientInformation clientInformation,
                               IArticleDownloaderEventSource? eventSource = null)
    {
        this.workingRoot = workingRoot;
        this.articleDatabase = articleDatabase;
        this.bookmarksClient = bookmarksClient;
        this.eventSource = eventSource;

        this.ImageClient = new Lazy<HttpClient>(() =>
        {
            HttpClient client = (handler is null) ? new HttpClient() : new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(clientInformation.UserAgent);

            return client;
        });
    }

    public void Dispose()
    {
        this.ImageClient.Value?.Dispose();
        this.ImageClient = new Lazy<HttpClient>();
    }

    private DatabaseLocalOnlyArticleState ApplyLocalStateToArticle(DatabaseArticle article, DatabaseLocalOnlyArticleState state)
    {
        // HasLocalState is only accurate at the time the instance of article
        // was read from the database. It's possible something has written the
        // local state since then, and we need to update, rather than insert.
        if (this.articleDatabase.GetLocalOnlyStateByArticleId(article.Id) is not null)
        {
            return this.articleDatabase.UpdateLocalOnlyArticleState(state);
        }

        return this.articleDatabase.AddLocalOnlyStateForArticle(state);
    }

    /// <summary>
    /// Given a set of articles, will download the body + images for those
    /// articles, updating the database along the way.
    /// </summary>
    /// <param name="articles">Articles to be downloaded</param>
    /// <returns>
    /// Task that completes when supplied articles &amp; their images
    /// completed
    /// </returns>
    internal async Task DownloadArticlesAsync(IEnumerable<DatabaseArticle> articles, CancellationToken cancellationToken = default)
    {
        this.eventSource?.RaiseDownloadingStarted(articles.Count());

        // Make sure we have an easy look up of articles. This is 'cause when we
        // get the local state back from the download, we don't know if we need
        // to Update or Add to the database. Having the article itself *does*
        // tell us that, which we can use later to make that decision
        var articleLookup = articles.ToDictionary((a) => a.Id)!;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localstates = new List<DatabaseLocalOnlyArticleState>();

            foreach (var article in articles)
            {
                try
                {
                    var localState = await this.DownloadArticleAsync(article, cancellationToken).ConfigureAwait(false);
                    if (localState is null)
                    {
                        continue;
                    }
                }
                catch (EntityNotFoundException)
                { /* If the article isn't found, we'll attempt another time */ }
            }
        }
        finally
        {
            this.eventSource?.RaiseDownloadingCompleted();
        }
    }

    /// <summary>
    /// Downloads articles which do not have any local state.
    /// </summary>
    /// <returns>Task that completes when articles have been processed</returns>
    internal async Task DownloadAllArticlesWithoutLocalStateAsync()
    {
        var articlesToDownload = this.articleDatabase.ListArticlesWithoutLocalOnlyState();
        if (articlesToDownload.Count() == 0)
        {
            return;
        }

        await this.DownloadArticlesAsync(articlesToDownload).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads the article from the service, processes any images (including
    /// downloading them) if present in the document. The updates are written to
    /// the database and returned, indicating the state of the article (e.g. was
    /// it available, any images, local paths etc).
    ///
    /// If a download for this article has already been started, a new one will
    /// not be started – the in-progress one will be returned instead.
    /// </summary>
    /// <param name="article">Article to download</param>
    /// <returns>Updated local state information</returns>
    public Task<DatabaseLocalOnlyArticleState?> DownloadArticleAsync(DatabaseArticle article, CancellationToken cancellationToken = default)
    {
        Task<DatabaseLocalOnlyArticleState?>? downloadTask = null;
        // We want to make sure we only have one download per article at any
        // given time. Two articles can be concurrent, but if we've started the
        // download for a specific article, lets not start another. We still want
        // callers to await the result, so we're going to squirrel away the task
        // if there isn't already one. To do that, we need a lock -- other wise
        // it'll be chaos.
        lock (this.inProgressDownloads)
        {
            // Only if we didn't find a task do we need to create one.
            if (!this.inProgressDownloads.TryGetValue(article.Id, out downloadTask))
            {
                // Note: We are *not* awaiting the task here, just initating it.
                // We'll insert it in the dictionary and return it to the caller
                // who will do the await.
                downloadTask = this.DownloadArticleCoreAsync(article, cancellationToken);
                this.inProgressDownloads[article.Id] = downloadTask;

                // But wait, I hear you ask. Where is it *removed* from the
                // dictionary? Thats actually handled in
                // DownloadArticleCoreAsync so that it's guarenteed to clean up
                // in the face of errors -- using ContinueWith requires handling
                // of cancellations, exceptions, etc. It seems simpler to do
                // it in that one location, rather than effectively duplicate it
                // here.
            }
        }

        return downloadTask;
    }

    private async Task<DatabaseLocalOnlyArticleState?> DownloadArticleCoreAsync(DatabaseArticle article, CancellationToken cancellationToken)
    {
        var articleDownloaded = true;
        var contentsUnavailable = false;
        Uri? localPath = null;
        string extractedDescription = String.Empty;
        FirstImageInformaton? firstImage = null;

        try
        {
            // Make sure you raise this before checking for cancellation. Tests
            // make use of this opportunity to inject failure to test certain
            // scenarios.
            this.eventSource?.RaiseArticleStarted(article);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var articleFilename = $"{article.Id}.html";
                var articleAbsoluteFilePath = Path.Combine(this.workingRoot, articleFilename);

                // Get the document contents, and process it
                var body = await this.bookmarksClient.GetTextAsync(article.Id).ConfigureAwait(false);
                (body, extractedDescription, firstImage) = await this.ProcessArticleAsync(body, article.Id, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                File.WriteAllText(articleAbsoluteFilePath, body, Encoding.UTF8);

                // We have successfully processed the article, so we can update the
                // state that'll be written to the database later
                localPath = new Uri(articleFilename, UriKind.Relative);
                articleDownloaded = true;
            }
            catch (BookmarkContentsUnavailableException)
            {
                // Contents weren't available, so we should mark it as a failure
                articleDownloaded = false;
                contentsUnavailable = true;
            }

            var localState = new DatabaseLocalOnlyArticleState()
            {
                ArticleId = article.Id,
                AvailableLocally = articleDownloaded,
                ArticleUnavailable = contentsUnavailable,
                LocalPath = localPath,
                ExtractedDescription = extractedDescription,
                FirstImageLocalPath = firstImage?.FirstLocalImage,
                FirstImageRemoteUri = firstImage?.FirstRemoteImage
            };

            return this.ApplyLocalStateToArticle(article, localState);
        }
        catch (TaskCanceledException)
        {
            // If the network request itself failed because of a cancel-like
            // case (e.g. timeout, network failure), map to a cancelled
            // operation so task-infra handles it right.
            throw new OperationCanceledException();
        }
        finally
        {
            // Make sure we remove any tasks for this article ID from the in
            // progress list to ensure that if a download is requested *after*
            // completion, we'll actually do the work there.
            lock (this.inProgressDownloads)
            {
                this.inProgressDownloads.Remove(article.Id);
            }

            this.eventSource?.RaiseArticleCompleted(article);
        }
    }

    /// <summary>
    /// Processes the article body prior to being written to disk
    /// </summary>
    /// <param name="body">HTML body to process</param>
    /// <param name="articleId">Article ID we're processing</param>
    /// <returns>Processed body with the required changes</returns>
    private async Task<ProcessedArticleInformation> ProcessArticleAsync(string body, long articleId, CancellationToken cancellationToken)
    {
        var configuration = ArticleDownloader.ParserConfiguration;

        // Load the document
        using var context = BrowsingContext.New(configuration);
        using var document = await context.OpenAsync(req => req.Content(body)).ConfigureAwait(false);

        this.eventSource?.RaiseImagesStarted(articleId);
        FirstImageInformaton? firstImage = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            firstImage = await ProcessAndDownloadImagesAsync(document, articleId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.eventSource?.RaiseImagesCompleted(articleId);
        }

        // We sometimes need a textual description of the article derived from
        // the content body. We do that by extracting up to the first 400 chars
        // from the *text* of the body (E.g. exclude markup), and stuffing that
        // in the database.
        var documentTextContent = document.Body!.GetInnerText() ?? String.Empty;
        var extractedDescription = documentTextContent.Substring(0, Math.Min(documentTextContent.Length, 400));

        // We don't want the document to be a 'full' document -- that'll be
        // handled by the consumer of the file at rendering time, rather than
        // having that baked into the actual on-disk file. So, we'll attempt to
        // mimic what the service returns (just the body). Note that even in a
        // 'no changes' case the service data and this data may not match, as
        // AngleSharp will attempt to generate stricter markup than it accepts
        return new(document.Body!.OuterHtml, extractedDescription, firstImage);
    }

    /// <summary>
    /// Processes images seen in the document, and downloads them. During this
    /// the document is updated to have a relative file path for the images,
    /// replacing the absolute URL that was orignally present.
    /// </summary>
    /// <param name="document">Document to download images for</param>
    /// <param name="articleId">Article ID we're processing </param>
    private async Task<FirstImageInformaton?> ProcessAndDownloadImagesAsync(IDocument document, long articleId, CancellationToken cancellationToken)
    {
        var images = document.QuerySelectorAll<IHtmlImageElement>("img[src^='http']");
        DirectoryInfo? imagesFolder = null;
        FirstImageInformaton? firstImage = null;

        int imageIndex = 0;

        foreach (var imageBatch in images.Chunkify(5))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (imagesFolder is null)
            {
                imagesFolder = Directory.CreateDirectory(Path.Combine(this.workingRoot, articleId.ToString()));
            }

            // For all the images in this batch, we'll initiate the tasks,
            // *before* awaiting them. This allows the network requests to
            // proceed concucrrentky. Once intiated, we'll wait for all to
            // complete before processing them further.
            List<Task<FirstImageInformaton?>> workerBatch = new List<Task<FirstImageInformaton?>>();
            foreach (var image in imageBatch)
            {
                workerBatch.Add(this.DownloadImageForElementAsync(image, imageIndex += 1, imagesFolder, cancellationToken));
            }

            await Task.WhenAll(workerBatch).ConfigureAwait(false);

            if (firstImage is null)
            {
                firstImage = workerBatch.Select((t) => t.Result).OfType<FirstImageInformaton>().DefaultIfEmpty(null).First();
            }
        }

        return firstImage;
    }

    /// <summary>
    /// Deletes any downloaded articles that are not currently present in the
    /// database. This includes any images thave have been downloaded.
    /// 
    /// It is intended to be called while the database is in a stable state, and
    /// there are no article downloads in progress.
    /// </summary>
    public void DeleteDownloadsWithNoDatabaseArticle()
    {
        var articles = new HashSet<long>(this.articleDatabase.ListAllArticlesInAFolder().Select((a) => a.Article.Id));
        articles.UnionWith(this.articleDatabase.ListArticlesNotInAFolder().Select((a) => a.Id));

        var articleFiles = Directory.GetFiles(this.workingRoot, "*.html");

        foreach (var filePath in articleFiles)
        {
            var filename = Path.GetFileNameWithoutExtension(filePath);
            if (!long.TryParse(filename, out long result))
            {
                // Not a valid article ID
                continue;
            }

            if (articles.Contains(result))
            {
                // Article is in the database, just keep it
                continue;
            }

            File.Delete(filePath);
            try
            {
                Directory.Delete(Path.Combine(this.workingRoot, result.ToString()), true);
            }
            catch { /* If we can't delete, c'est le vie */ }
        }
    }

    private async Task<FirstImageInformaton?> DownloadImageForElementAsync(IHtmlImageElement image, int imageIndex, DirectoryInfo imagesFolder, CancellationToken cancellationToken)
    {
        if (!Uri.IsWellFormedUriString(image.Source!, UriKind.Absolute))
        {
            return null;
        }

        var imageUri = new Uri(image.Source!, UriKind.Absolute);
        if (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // 1. Compute the temporary target file name on the *image index*
        //    e.g. which Image we're processing, since the file name from
        //    the source may not be writable locally
        var tempFilename = $"{imageIndex}.temp-image";

        // 2. Compute the absolute local path to download to
        var tempFilepath = Path.Combine(imagesFolder.FullName, tempFilename);

        // 3. Download the image locally
        this.eventSource?.RaiseImageStarted(imageUri);
        try
        {
            var imageRequestTask = this.ImageClient.Value.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            string contentType = "image/unknown";
            using (var targetStream = File.Open(tempFilepath, FileMode.Create, FileAccess.Write))
            {
                using var imageResponse = await imageRequestTask;
                if (!imageResponse.IsSuccessStatusCode)
                {
                    // If this was a failed request for the image, we
                    // need to clean up the temporary file we created
                    targetStream.Close();
                    File.Delete(tempFilepath);
                    return null;
                }

                contentType = imageResponse.Content.Headers.ContentType.MediaType;
                await imageResponse.Content.CopyToAsync(targetStream).ConfigureAwait(false);
            }

            // 4. Identify the image format & metadata
            string extension = "unknown";
            var (imageInfo, imageFormat) = await Image.IdentifyWithFormatAsync(tempFilepath, cancellationToken).ConfigureAwait(false);
            if (imageFormat is null)
            {
                // We couldn't identify it, so we'd better check to see if
                // it's an SVG. We're going to rely on the content type
                if (contentType != SVG_CONTENT_TYPE)
                {
                    File.Delete(tempFilepath);
                    return null;
                }

                extension = "svg";
            }
            else
            {
                extension = imageFormat.FileExtensions.First();
                // Image sharp thinks 'bm' is a valid bitmap extension
                // and we don't. So lets fix it up.
                if (extension == "bm")
                {
                    extension = "bmp";
                }
            }

            // 5. Move the file into the final destination
            Debug.Assert(extension != "unknown", "Shouldn't have an unkown file extension");
            var filename = Path.ChangeExtension(tempFilename, extension);
            var filePath = Path.Combine(imagesFolder.FullName, filename);

            // If we're redownloading the article, the image might already
            // exist. Since we _move_ the file into position, and there is
            // no overwrite option in this .net verison, we have to try
            // deleting it -- which doesn't error if the file isn't present
            File.Delete(filePath);

            File.Move(tempFilepath, filePath);

            // 6. Rewrite the src attribute on the image to the *relative*
            //    path that we've calculated
            var relativePath = $"{imagesFolder.Name}/{filename}";
            var originalUrl = image.Source;
            image.Source = relativePath;

            if (imageFormat is null && contentType != SVG_CONTENT_TYPE)
            {
                // We have no image details to process, but we downloaded it
                // anyway -- maybe the UI can decode it in a different
                // context
                return null;
            }

            // 7. Create first image information to allow caller to pick
            // SVG doesn't have dimensions, per-se. They also render
            // well being vector-images. For non-svg images, we don't
            // want small images
            if (contentType != SVG_CONTENT_TYPE)
            {
                if (imageInfo.Width < MINIMUM_IMAGE_DIMENSION || imageInfo.Height < MINIMUM_IMAGE_DIMENSION)
                {
                    return null;
                }
            }

            return new FirstImageInformaton(
                new Uri(relativePath, UriKind.Relative),
                new Uri(originalUrl, UriKind.Absolute)
            );
        }
        finally
        {
            this.eventSource?.RaiseImageCompleted(imageUri);
        }
    }
}