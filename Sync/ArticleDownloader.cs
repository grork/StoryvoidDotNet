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

    /// <summary>
    /// Placeholder base URI that local paths will be relative to. This is
    /// because the local root is variable, and we dont want to encode that path
    /// in the database.
    /// </summary>
    public static readonly Uri ROOT_URI = new Uri("localfile://local");

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

    /// <summary>
    /// Downloads the bookmark from the service, processes any images (including
    /// downloading them) if present in the document. The updates are written to
    /// the database and returned, indicating the state of the article (e.g. was
    /// it available, any images, local paths etc)
    /// </summary>
    /// <param name="bookmarkId">ID of the bookmark to download</param>
    /// <returns>Updated local state information</returns>
    public async Task<DatabaseLocalOnlyArticleState?> DownloadBookmark(long bookmarkId)
    {
        var articleDownloaded = true;
        var contentsUnavailable = false;
        Uri? localPath = null;
        string extractedDescription = String.Empty;
        FirstImageInformaton? firstImage = null;
        DownloadArticleArgs? eventInformation = null;

        var articleInformation = this.articleDatabase.GetArticleById(bookmarkId);
        if(articleInformation == null)
        {
            return null;
        }

        if (this.eventSource != null)
        {
            eventInformation = new(bookmarkId, articleInformation.Title);
            this.eventSource.RaiseArticleStarted(eventInformation);
        }

        try
        {
            var bookmarkFileName = $"{bookmarkId}.html";
            var bookmarkAbsoluteFilePath = Path.Combine(this.workingRoot, bookmarkFileName);

            // Get the document contents, and process it
            var body = await this.bookmarksClient.GetTextAsync(bookmarkId);
            (body, extractedDescription, firstImage) = await this.ProcessArticle(body, bookmarkId);

            File.WriteAllText(bookmarkAbsoluteFilePath, body, Encoding.UTF8);

            // We have successfully processed the article, so we can update the
            // state that'll be written to the database later
            localPath = new Uri(ROOT_URI, bookmarkFileName);
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
            ArticleId = bookmarkId,
            AvailableLocally = articleDownloaded,
            ArticleUnavailable = contentsUnavailable,
            LocalPath = localPath,
            ExtractedDescription = extractedDescription,
            FirstImageLocalPath = firstImage?.FirstLocalImage,
            FirstImageRemoteUri = firstImage?.FirstRemoteImage
        };

        var localStatePresent = (articleDatabase.GetLocalOnlyStateByArticleId(bookmarkId) != null);
        if(localStatePresent)
        {
            localState = articleDatabase.UpdateLocalOnlyArticleState(localState);
        }
        else
        {
            localState = articleDatabase.AddLocalOnlyStateForArticle(localState);
        }

        if (eventInformation != null)
        {
            this.eventSource?.RaiseArticleCompleted(eventInformation);
        }

        return localState;
    }

    /// <summary>
    /// Processes the article body prior to being written to disk
    /// </summary>
    /// <param name="body">HTML body to process</param>
    /// <param name="bookmarkId">Article ID we're processing</param>
    /// <returns>Processed body with the required changes</returns>
    private async Task<ProcessedArticleInformation> ProcessArticle(string body, long bookmarkId)
    {
        var configuration = ArticleDownloader.ParserConfiguration;

        // Load the document
        using var context = BrowsingContext.New(configuration);
        using var document = await context.OpenAsync(req => req.Content(body));

        this.eventSource?.RaiseImagesStarted(bookmarkId);
        FirstImageInformaton? firstImage = null;
        try
        {
            firstImage = await ProcessAndDownloadImages(document, bookmarkId);
        }
        finally
        {
            this.eventSource?.RaiseImagesCompleted(bookmarkId);
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
    /// <param name="bookmarkId">Bookmark ID we're processing </param>
    private async Task<FirstImageInformaton?> ProcessAndDownloadImages(IDocument document, long bookmarkId)
    {
        var images = document.QuerySelectorAll<IHtmlImageElement>("img[src^='http']");
        DirectoryInfo? imageDirectory = null;
        FirstImageInformaton? firstImage = null;

        int imageIndex = 1;

        foreach (var imageBatch in images.Chunkify(5))
        {
            if (imageDirectory == null)
            {
                imageDirectory = Directory.CreateDirectory(Path.Combine(this.workingRoot, bookmarkId.ToString()));
            }

            foreach (var image in imageBatch)
            {
                if (!Uri.IsWellFormedUriString(image.Source!, UriKind.Absolute))
                {
                    continue;
                }

                var imageUri = new Uri(image.Source!, UriKind.Absolute);
                if (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps)
                {
                    continue;
                }

                // 1. Compute the temporary target file name on the *image index*
                //    e.g. which Image we're processing, since the file name from
                //    the source may not be writable locally
                var tempFilename = $"{imageIndex++}.temp-image";

                // 2. Compute the absolute local path to download to
                var tempFilepath = Path.Combine(imageDirectory.FullName, tempFilename);

                // 3. Download the image locally
                this.eventSource?.RaiseImageStarted(imageUri);
                try
                {
                    var imageRequestTask = this.ImageClient.Value.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead);
                    string contentType = "image/unknown";
                    using (var targetStream = File.Open(tempFilepath, FileMode.Create, FileAccess.Write))
                    {
                        using var requestStream = await imageRequestTask;
                        contentType = requestStream.Content.Headers.ContentType.MediaType;
                        await requestStream.Content.CopyToAsync(targetStream);
                    }

                    // 4. Identify the image format & metadata
                    string extension = "unknown";
                    var (imageInfo, imageFormat) = await Image.IdentifyWithFormatAsync(tempFilepath);
                    if (imageFormat == null)
                    {
                        // We couldn't identify it, so we'd better check to see if
                        // it's an SVG. We're going to rely on the content type
                        if (contentType != SVG_CONTENT_TYPE)
                        {
                            File.Delete(tempFilepath);
                            continue;
                        }

                        extension = "svg";
                    }
                    else
                    {
                        extension = imageFormat.FileExtensions.First();
                        // Image sharp thinks 'bm' is a valid bitmap extension
                        // and we don't. So lets fix it up.
                        if(extension == "bm")
                        {
                            extension = "bmp";
                        }
                    }

                    // 5. Move the file into the final destination
                    Debug.Assert(extension != "unknown", "Shouldn't have an unkown file extension");
                    var filename = Path.ChangeExtension(tempFilename, extension);
                    var filePath = Path.Combine(imageDirectory.FullName, filename);

                    // If we're redownloading the article, the image might already
                    // exist. Since we _move_ the file into position, and there is
                    // no overwrite option in this .net verison, we have to try
                    // deleting it -- which doesn't error if the file isn't present
                    File.Delete(filePath);

                    File.Move(tempFilepath, filePath);

                    // 6. Rewrite the src attribute on the image to the *relative*
                    //    path that we've calculated
                    var relativePath = $"{imageDirectory.Name}/{filename}";
                    var originalUrl = image.Source;
                    image.Source = relativePath;

                    if (imageFormat == null && contentType != SVG_CONTENT_TYPE)
                    {
                        // We have no image details to process, but we downloaded it
                        // anyway -- maybe the UI can decode it in a different
                        // context
                        continue;
                    }

                    // 7. Select a first image, if we don't currently have one
                    if (firstImage == null)
                    {
                        // SVG doesn't have dimensions, per-se. They also render
                        // well being vector-images. For non-svg images, we don't
                        // want small images
                        if (contentType != SVG_CONTENT_TYPE)
                        {
                            if (imageInfo.Width < MINIMUM_IMAGE_DIMENSION || imageInfo.Height < MINIMUM_IMAGE_DIMENSION)
                            {
                                continue;
                            }
                        }

                        firstImage = new FirstImageInformaton(
                            new Uri(relativePath, UriKind.Relative),
                            new Uri(originalUrl, UriKind.Absolute)
                        );
                    }
                }
                finally
                {
                    this.eventSource?.RaiseImageCompleted(imageUri);
                }
            }
        }

        return firstImage;
    }
}