﻿using AngleSharp;
using AngleSharp.Dom;
using System.Text;
using Codevoid.Instapaper;

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
        while(enumerator.MoveNext())
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
public class ArticleDownloader
{
    /// <summary>
    /// Placeholder base URI that local paths will be relative to. This is
    /// because the local root is variable, and we dont want to encode that path
    /// in the database.
    /// </summary>
    public static readonly Uri ROOT_URI = new Uri("localfile://local");

    
    private static Lazy<IConfiguration> ParserConfiguration = new Lazy<IConfiguration>(() =>
    {
        // Remove 'dangerous' aspects of AngleSharps API so bad things can't
        // happen
        var configuration = Configuration.Default.WithCss(); // Lets us use GetInnerText()
        configuration = configuration.Without<AngleSharp.Dom.Events.IEventFactory>();
        configuration = configuration.Without<AngleSharp.Dom.IAttributeObserver>();
        configuration = configuration.Without<AngleSharp.Browser.INavigationHandler>();

        return configuration;
    }, true);

    private string workingRoot;
    private IArticleDatabase articleDatabase;
    private IBookmarksClient bookmarksClient;

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
    public ArticleDownloader(string workingRoot,
                             IArticleDatabase articleDatabase,
                             IBookmarksClient bookmarksClient)
    {
        this.workingRoot = workingRoot;
        this.articleDatabase = articleDatabase;
        this.bookmarksClient = bookmarksClient;
    }

    /// <summary>
    /// Downloads the bookmark from the service, processes any images (including
    /// downloading them) if present in the document. The updates are written to
    /// the database and returned, indicating the state of the article (e.g. was
    /// it available, any images, local paths etc)
    /// </summary>
    /// <param name="bookmarkId">ID of the bookmark to download</param>
    /// <returns>Updated local state information</returns>
    public async Task<DatabaseLocalOnlyArticleState> DownloadBookmark(long bookmarkId)
    {
        var articleDownloaded = true;
        var contentsUnavailable = false;
        Uri? localPath = null;
        string extractedDescription = String.Empty;

        try
        {
            var bookmarkFileName = $"{bookmarkId}.html";
            var bookmarkAbsoluteFilePath = Path.Combine(workingRoot, bookmarkFileName);

            // Get the document contents, and process it
            var body = await this.bookmarksClient.GetTextAsync(bookmarkId);
            (body, extractedDescription) = await this.ProcessArticle(body);

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

        return articleDatabase.AddLocalOnlyStateForArticle(new()
        {
            ArticleId = bookmarkId,
            AvailableLocally = articleDownloaded,
            ArticleUnavailable = contentsUnavailable,
            LocalPath = localPath,
            ExtractedDescription = extractedDescription
        });
    }

    /// <summary>
    /// Processes the article body prior to being written to disk
    /// </summary>
    /// <param name="body">HTML body to process</param>
    /// <returns>Processed body with the required changes</returns>
    private async Task<(string Body, string ExtractedDescription)> ProcessArticle(string body)
    {
        var configuration = ArticleDownloader.ParserConfiguration.Value;

        // Load the document
        var context = BrowsingContext.New(configuration);
        var document = await context.OpenAsync(req => req.Content(body));

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
        return (document.Body!.OuterHtml, extractedDescription);
    }
}