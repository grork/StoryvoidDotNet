using System.Data;
using System.Diagnostics.CodeAnalysis;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Codevoid.Test.Storyvoid.Sync;

public class ArticleDownloaderTests : IDisposable
{
    private readonly IDbConnection connection;
    private readonly IArticleDatabase articleDatabase;
    private ArticleDownloader articleDownloader;
    private readonly IBookmarksClient bookmarkClient;
    private readonly Codevoid.Utilities.OAuth.ClientInformation clientInformation;
    private readonly FileSystemMappedHttpHandler localFileHttpHandler;

    private readonly DirectoryInfo testDirectory;
    private readonly string mockResponsesFolder = Path.Join(Environment.CurrentDirectory, "mockbookmarkresponses");

    #region Mock Article IDs
    private const long AWOL_EVERYWHERE_ARTICLE = 8L;
    private const long MISSING_REMOTE_ARTICLE = 9L;
    private const long BASIC_ARTICLE_NO_IMAGES = 10L;
    private const long UNAVAILABLE_ARTICLE = 11L;
    private const long LARGE_ARTICLE_NO_IMAGES = 12L;
    private const long SHORT_ARTICLE_NO_IMAGES = 14L;
    private const long EMPTY_ARTICLE = 15L;
    private const long YOUTUBE_ARTICLE = 16L;
    private const long IMAGES_ARTICLE = 17L;
    private const long IMAGES_WITH_QUERY_STRINGS = 18L;
    private const long IMAGES_WITH_INLINE_IMAGES = 19L;
    private const long FIRST_IMAGE_PNG = 20L;
    private const long FIRST_IMAGE_ANIMATED_PNG = 21L;
    private const long FIRST_IMAGE_GIF = 22L;
    private const long FIRST_IMAGE_ANIMATED_GIF = 23L;
    private const long FIRST_IMAGE_JPG = 24L;
    private const long FIRST_IMAGE_SVG = 25L;
    private const long FIRST_IMAGE_WEBP = 26L;
    private const long FIRST_IMAGE_LESS_THAN_150PX = 27L;
    private const long MISSING_IMAGE = 28L;
    #endregion

    public ArticleDownloaderTests()
    {
        var testFolderId = Guid.NewGuid().ToString();
        this.testDirectory = Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, testFolderId));

        var (connection, _, _, articleDatabase, _) = TestUtilities.GetEmptyDatabase();
        this.connection = connection;
        this.articleDatabase = articleDatabase;
        this.clientInformation = Test.Instapaper.TestUtilities.GetClientInformation();

        var fileMap = PopulateDownloadableArticles();
        this.bookmarkClient = new MockBookmarkServiceWithOnlyGetText(fileMap);

        var samplesFolder = Path.Join(Environment.CurrentDirectory, "samplepages");
        this.localFileHttpHandler = new FileSystemMappedHttpHandler(Directory.CreateDirectory(samplesFolder));
        this.ResetArticleDownloader();
    }

    [MemberNotNull(nameof(articleDownloader))]
    private void ResetArticleDownloader(IArticleDownloaderEventSource? eventSource = null)
    {
        this.articleDownloader = new ArticleDownloader(
            this.localFileHttpHandler,
            this.testDirectory.FullName,
            this.articleDatabase,
            this.bookmarkClient,
            clientInformation,
            eventSource
        );
    }

    private IDictionary<long, string> PopulateDownloadableArticles()
    {
        IDictionary<long, string> articleFileMap = new Dictionary<long, string>();

        void AddArticle(long id, string filename, string title)
        {
            var article = new ArticleRecordInformation(
                id: id,
                title: title,
                url: new Uri(TestUtilities.BASE_URL, $"/{id}"),
                description: String.Empty,
                readProgress: 0.0F,
                readProgressTimestamp: DateTime.Now,
                hash: "ABC",
                liked: false
            );

            this.articleDatabase.AddArticleToFolder(article, WellKnownLocalFolderIds.Unread);

            articleFileMap.Add(id, (!String.IsNullOrWhiteSpace(filename) ? Path.Join(this.mockResponsesFolder, filename) : filename));
        }

        AddArticle(UNAVAILABLE_ARTICLE, String.Empty, "Article with no content available");
        AddArticle(BASIC_ARTICLE_NO_IMAGES, "BasicArticleNoImage.html", "Basic Article Without Images");
        AddArticle(LARGE_ARTICLE_NO_IMAGES, "LargeArticleNoImage.html", "Large Article Without Images");
        AddArticle(SHORT_ARTICLE_NO_IMAGES, "ShortArticleNoImage.html", "Short Article Without Images");
        AddArticle(EMPTY_ARTICLE, "EmptyArticle.html", "Empty Article");
        AddArticle(YOUTUBE_ARTICLE, "youtube.html", "YouTube");
        AddArticle(IMAGES_ARTICLE, "ArticleWithImagesAlt.html", "Article With Images");
        AddArticle(IMAGES_WITH_QUERY_STRINGS, "ArticleWithImagesAndQueryStrings.html", "Article With Images that have query strings in their URLs");
        AddArticle(IMAGES_WITH_INLINE_IMAGES, "ArticleWithInlineDataImages.html", "Article with Inline images");
        AddArticle(FIRST_IMAGE_PNG, "ArticleWithPNGFirstImage.html", "Article with PNG as first image");
        AddArticle(FIRST_IMAGE_ANIMATED_PNG, "ArticleWithAnimatedPNGFirstImage.html", "Article with animated PNG as first image");
        AddArticle(FIRST_IMAGE_GIF, "ArticleWithStaticGIFFirstImage.html", "Article with GIF as first image");
        AddArticle(FIRST_IMAGE_ANIMATED_GIF, "ArticleWithAnimatedGIFFirstImage.html", "Article with Animated GIF as first image");
        AddArticle(FIRST_IMAGE_JPG, "ArticleWithJPGFirstImage.html", "Article with JPG as first image");
        AddArticle(FIRST_IMAGE_SVG, "ArticleWithSVGFirstImage.html", "Article with SVG as first image");
        AddArticle(FIRST_IMAGE_WEBP, "ArticleWithWEBPFirstImage.html", "Article with WEBP as first image");
        AddArticle(FIRST_IMAGE_LESS_THAN_150PX, "ArticleWithFirstImageLessThan150px.html", "Article with first image < 150px");
        AddArticle(MISSING_IMAGE, "ArticleWithMissingImage.html", "Article with a missing image");

        // Special case for an article that is available in the database, but
        // isn't on the service. We don't want to add a file mapping in that case
        this.articleDatabase.AddArticleToFolder(new(
                id: MISSING_REMOTE_ARTICLE,
                title: "Article not present remotely",
                url: new Uri(TestUtilities.BASE_URL, $"/{MISSING_REMOTE_ARTICLE}"),
                description: String.Empty,
                readProgress: 0.0F,
                readProgressTimestamp: DateTime.Now,
                hash: "ABC",
                liked: false
            ), WellKnownLocalFolderIds.Unread);

        return articleFileMap;
    }

    private Uri GetRelativeUriForDownloadedArticle(long articleId)
    {
        return new Uri(ArticleDownloader.ROOT_URI, $"{articleId}.html");
    }

    public void Dispose()
    {
        try
        {
            this.testDirectory.Delete(true);
        }
        catch (IOException) { }

        this.connection.Close();
        this.connection.Dispose();

        this.articleDownloader.Dispose();
    }

    private void AssertAvailableLocallyAndFileExists(DatabaseLocalOnlyArticleState localState)
    {
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState!.ArticleId), localState!.LocalPath);
        Assert.True(localState!.AvailableLocally);
        Assert.False(localState!.ArticleUnavailable);

        var fileExists = File.Exists(Path.Join(this.testDirectory.FullName, localState!.LocalPath!.AbsolutePath));
        Assert.True(fileExists);
    }

    #region Basic Downloading
    [Fact]
    public async Task CanDownloadArticleWithoutImages()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        AssertAvailableLocallyAndFileExists(localState!);
    }

    [Fact]
    public async Task LocalFileContainsOnlyTheBody()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState!.ArticleId), localState!.LocalPath);
        Assert.True(localState!.AvailableLocally);
        Assert.False(localState!.ArticleUnavailable);

        var localContents = File.ReadAllText(Path.Join(this.testDirectory.FullName, localState!.LocalPath!.AbsolutePath));

        // We want to make sure that we don't turn this into a fully fledges HTML
        // document, as the consumer is expected to perform the appropriate
        // cleanup
        Assert.StartsWith("<body>", localContents);
        Assert.EndsWith("</body>", localContents);
    }

    [Fact]
    public async Task CanDownloadLargeArticleWithoutImages()
    {
        var localState = await this.articleDownloader.DownloadBookmark(LARGE_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState!.ArticleId), localState!.LocalPath);
        Assert.True(localState!.AvailableLocally);
        Assert.False(localState!.ArticleUnavailable);

        var fileExists = File.Exists(Path.Join(this.testDirectory.FullName, localState!.LocalPath!.AbsolutePath));
        Assert.True(fileExists);
    }

    [Fact]
    public async Task DownloadingUnavailableArticleMarksArticleAsUnavailable()
    {
        var localState = await this.articleDownloader.DownloadBookmark(UNAVAILABLE_ARTICLE);
        Assert.Null(localState!.LocalPath);
        Assert.False(localState!.AvailableLocally);
        Assert.True(localState!.ArticleUnavailable);
    }

    [Fact]
    public async Task DownloadingArticleThatIsntOnTheServiceFails()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => this.articleDownloader.DownloadBookmark(MISSING_REMOTE_ARTICLE));
        var localState = this.articleDatabase.GetLocalOnlyStateByArticleId(MISSING_REMOTE_ARTICLE);
        Assert.Null(localState);
    }

    [Fact]
    public async Task DownloadingArticleThatIsNotLocalOrRemoteShouldNotErrorOrLeaveBadData()
    {
        var result = await this.articleDownloader.DownloadBookmark(AWOL_EVERYWHERE_ARTICLE);
        Assert.Null(result);

        var localState = this.articleDatabase.GetLocalOnlyStateByArticleId(MISSING_REMOTE_ARTICLE);
        Assert.Null(localState);
    }

    [Fact]
    public async Task ArticleTextIsExtractedFromBodyText()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.NotEmpty(localState!.ExtractedDescription);
        Assert.Equal(400, localState!.ExtractedDescription.Length);
    }

    [Fact]
    public async Task ArticleTextIsExtractedFromBodyTextWhenShort()
    {
        var localState = await this.articleDownloader.DownloadBookmark(SHORT_ARTICLE_NO_IMAGES);
        Assert.NotEmpty(localState!.ExtractedDescription);
        Assert.Equal(15, localState!.ExtractedDescription.Length);
    }

    [Fact]
    public async Task CanDownloadArticleWithEmptyBody()
    {
        _ = await this.articleDownloader.DownloadBookmark(EMPTY_ARTICLE);
    }

    [Fact]
    public async Task DescriptionExtractedFromYouTube()
    {
        var localState = await this.articleDownloader.DownloadBookmark(YOUTUBE_ARTICLE);
        Assert.Empty(localState!.ExtractedDescription);
    }

    [Fact]
    public async Task MegaTransactionRollsBackSingleArticle()
    {
        using (var transaction = this.connection.BeginTransaction())
        {

            var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
            Assert.NotNull(localState);

            var retrievedLocalState = this.articleDatabase.GetLocalOnlyStateByArticleId(BASIC_ARTICLE_NO_IMAGES);
            Assert.NotNull(retrievedLocalState);

            transaction.Rollback();
        }

        var afterRollbackLocalState = this.articleDatabase.GetLocalOnlyStateByArticleId(BASIC_ARTICLE_NO_IMAGES);
        Assert.Null(afterRollbackLocalState);
    }
    #endregion

    #region Image Processing
    private async Task BasicImageDownloadTest(long articleId, int expectedImageCount)
    {
        var localState = await this.articleDownloader.DownloadBookmark(articleId);
        var imagesPath = Path.Join(this.testDirectory.FullName, localState!.ArticleId.ToString());
        Assert.True(Directory.Exists(imagesPath));
        Assert.Equal(expectedImageCount, Directory.GetFiles(imagesPath).Count());

        var localPath = Path.Join(this.testDirectory.FullName, localState!.LocalPath!.AbsolutePath);
        var htmlParserContext = BrowsingContext.New(ArticleDownloader.ParserConfiguration);
        var document = await htmlParserContext.OpenAsync((r) => r.Content(File.Open(localPath, FileMode.Open, FileAccess.Read), true));

        var seenImages = 0;
        foreach (var image in document.QuerySelectorAll<IHtmlImageElement>("img[src]"))
        {
            var imageSrc = image.GetAttribute("src")!;
            if (imageSrc!.StartsWith("http"))
            {
                // We only look for processed images
                continue;
            }

            seenImages += 1;

            var imagePath = Path.Combine(this.testDirectory.FullName, imageSrc);
            Assert.True(File.Exists(imagePath));
        }

        Assert.Equal(expectedImageCount, seenImages);
    }

    [Fact]
    public async Task CanDownloadArticleWithImages()
    {
        await BasicImageDownloadTest(
            articleId: IMAGES_ARTICLE,
            expectedImageCount: 17
        );
    }

    [Fact]
    public async Task CanDownloadArticleWithImagesThatHaveQueryStringsInTheirUrls()
    {
        await BasicImageDownloadTest(
            articleId: IMAGES_WITH_QUERY_STRINGS,
            expectedImageCount: 16
        );
    }

    [Fact]
    public async Task InlineImagesAreLeftUntouchedAndDontDownloadAnything()
    {
        const int EXPECTED_IMAGE_COUNT = 2;
        var localState = await this.articleDownloader.DownloadBookmark(IMAGES_WITH_INLINE_IMAGES);
        var imagesPath = Path.Join(this.testDirectory.FullName, localState!.ArticleId.ToString());
        Assert.False(Directory.Exists(imagesPath));

        var localPath = Path.Join(this.testDirectory.FullName, localState!.LocalPath!.AbsolutePath);
        var htmlParserContext = BrowsingContext.New(ArticleDownloader.ParserConfiguration);
        var document = await htmlParserContext.OpenAsync((r) => r.Content(File.Open(localPath, FileMode.Open, FileAccess.Read), true));

        var seenImages = 0;
        foreach (var image in document.QuerySelectorAll<IHtmlImageElement>("img[src]"))
        {
            var imageSrc = image.GetAttribute("src")!;
            Assert.StartsWith("data:", imageSrc);

            seenImages += 1;
        }

        Assert.Equal(EXPECTED_IMAGE_COUNT, seenImages);
    }

    [Fact]
    public async Task NoThumbnailImageReturnedIfNoImagesInArticle()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.Null(localState!.FirstImageLocalPath);
        Assert.Null(localState!.FirstImageRemoteUri);
        Assert.False(localState!.HasImages);
    }

    [Fact]
    public async Task NoThumbnailImageReturnedIfOnlyInlineImagesInArticle()
    {
        var localState = await this.articleDownloader.DownloadBookmark(IMAGES_WITH_INLINE_IMAGES);
        Assert.Null(localState!.FirstImageLocalPath);
        Assert.Null(localState!.FirstImageRemoteUri);
        Assert.False(localState!.HasImages);
    }

    [Fact]
    public async Task FirstImageIsSelectedIfArticleHasImagesPng() => await FirstImageIsSelected(FIRST_IMAGE_PNG, "sample.png");

    [Fact]
    public async Task FirstImageIsSelectedIfArticleHasImagesGif() => await FirstImageIsSelected(FIRST_IMAGE_GIF, "sample-gif89a.gif");

    [Fact]
    public async Task FirstImageIsSelectedIfArticleHasImagesJpg() => await FirstImageIsSelected(FIRST_IMAGE_JPG, "sample.jpg");

    [Fact]
    public async Task FirstImageIsSelectedIfArticleHasImagesWebp() => await FirstImageIsSelected(FIRST_IMAGE_WEBP, "sample.webp");

    [Fact]
    public async Task FirstImageIsSelectedIfArticleHasImagesSvg() => await FirstImageIsSelected(FIRST_IMAGE_SVG, "sample.svg");

    private async Task FirstImageIsSelected(long articleId, string expectedFirstImage)
    {
        var localState = await this.articleDownloader.DownloadBookmark(articleId);
        Assert.NotNull(localState!.FirstImageLocalPath);
        Assert.NotNull(localState!.FirstImageRemoteUri);
        Assert.True(localState!.HasImages);

        var expectedLocalPath = new Uri($"{articleId}/1.{Path.GetExtension(expectedFirstImage).Substring(1)}", UriKind.Relative);
        var expectedRemotePath = new Uri($"https://www.codevoid.net/storyvoidtest/{expectedFirstImage}");

        Assert.Equal(expectedLocalPath, localState!.FirstImageLocalPath);
        Assert.Equal(expectedRemotePath, localState!.FirstImageRemoteUri);
    }

    [Fact]
    public async Task FirstImageUnder150pxIsNotSelected()
    {
        var localState = await this.articleDownloader.DownloadBookmark(FIRST_IMAGE_LESS_THAN_150PX);
        Assert.NotNull(localState!.FirstImageLocalPath);
        Assert.NotNull(localState!.FirstImageRemoteUri);
        Assert.True(localState!.HasImages);

        var expectedLocalPath = new Uri($"{FIRST_IMAGE_LESS_THAN_150PX}/2.png", UriKind.Relative);
        var expectedRemotePath = new Uri("https://www.codevoid.net/storyvoidtest/sample.png");

        Assert.Equal(expectedLocalPath, localState!.FirstImageLocalPath);
        Assert.Equal(expectedRemotePath, localState!.FirstImageRemoteUri);
    }

    [Fact]
    public async Task CanDownloadSameArticleTwice()
    {
        await BasicImageDownloadTest(
            articleId: IMAGES_ARTICLE,
            expectedImageCount: 17
        );

        await BasicImageDownloadTest(
            articleId: IMAGES_ARTICLE,
            expectedImageCount: 17
        );
    }

    [Fact]
    public async Task ArticleWithSomeMissingImagesDownloadsCorrectly()
    {
        await BasicImageDownloadTest(
            articleId: MISSING_IMAGE,
            expectedImageCount: 1
        );
    }
    #endregion

    #region Eventing
    [Fact]
    public async Task EventsRaisedForDownloadingAnArticle()
    {
        const string ARTICLE_TITLE = "Article With Images";
        var EXPECTED_DOWNLOAD_ARGS = new DownloadArticleArgs(IMAGES_ARTICLE, ARTICLE_TITLE);

        DownloadArticleArgs? articleStarting = null;
        var imagesStarted = -1L;
        var imageStarted = new List<Uri>();
        var imageCompleted = new List<Uri>();
        var imagesCompleted = -1L;
        DownloadArticleArgs? articleCompleted = null;

        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        this.ResetArticleDownloader(clearingHouse);

        clearingHouse.ArticleStarted += (_, args) => articleStarting = args;
        clearingHouse.ImagesStarted += (_, articleId) => imagesStarted = articleId;
        clearingHouse.ImageStarted += (_, uri) => imageStarted.Add(uri);
        clearingHouse.ImageCompleted += (_, uri) => imageCompleted.Add(uri);
        clearingHouse.ImagesCompleted += (_, articleId) => imagesCompleted = articleId;
        clearingHouse.ArticleCompleted += (_, args) => articleCompleted = args;

        await this.articleDownloader.DownloadBookmark(IMAGES_ARTICLE);

        Assert.Equal(EXPECTED_DOWNLOAD_ARGS, articleStarting);
        Assert.Equal(IMAGES_ARTICLE, imagesStarted);
        Assert.Equal(18, imageStarted.Count);
        Assert.Equal(imageStarted.Count, imageCompleted.Count);
        Assert.Equal(imageStarted, imageCompleted);
        Assert.Equal(IMAGES_ARTICLE, imagesCompleted);
        Assert.Equal(EXPECTED_DOWNLOAD_ARGS, articleCompleted);
    }

    [Fact]
    public async Task DownloadingMultipleArticleRasiesEvent()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            IMAGES_WITH_QUERY_STRINGS,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };

        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        var downloadStarted = false;
        var articlesStarted = new List<DownloadArticleArgs>();
        var imagesStarted = new List<long>();
        var imageStarted = new List<Uri>();
        var imageCompleted = new List<Uri>();
        var imagesCompleted = new List<long>();
        var articlesCompleted = new List<DownloadArticleArgs>();
        var downloadCompleted = false;

        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        this.ResetArticleDownloader(clearingHouse);

        clearingHouse.DownloadingStarted += (_, _) => downloadStarted = true;
        clearingHouse.ArticleStarted += (_, args) => articlesStarted.Add(args);
        clearingHouse.ImagesStarted += (_, articleId) => imagesStarted.Add(articleId);
        clearingHouse.ImageStarted += (_, uri) => imageStarted.Add(uri);
        clearingHouse.ImageCompleted += (_, uri) => imageCompleted.Add(uri);
        clearingHouse.ImagesCompleted += (_, articleId) => imagesCompleted.Add(articleId);
        clearingHouse.ArticleCompleted += (_, args) => articlesCompleted.Add(args);
        clearingHouse.DownloadingCompleted += (_, _) => downloadCompleted = true;

        await this.articleDownloader.DownloadBookmarks(articlesToDownload);

        Assert.True(downloadStarted);
        Assert.Equal(articleIds.Length, articlesStarted.Count);
        Assert.Equal(articleIds, imagesStarted);
        Assert.Equal(imageStarted, imageCompleted);
        Assert.Equal(articleIds, imagesCompleted);
        Assert.Equal(articleIds.Length, articlesCompleted.Count);
        Assert.True(downloadCompleted);
    }

    [Fact]
    public async Task DownloadingArticlesWithMissingRemoteArticleRaisesEvents()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            MISSING_REMOTE_ARTICLE,
            IMAGES_WITH_QUERY_STRINGS,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };

        var articleIdsWithoutMissingArticle = articleIds.Except(new long[] { MISSING_REMOTE_ARTICLE });

        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        var downloadStarted = false;
        var articlesStarted = new List<DownloadArticleArgs>();
        var imagesStarted = new List<long>();
        var imageStarted = new List<Uri>();
        var imageCompleted = new List<Uri>();
        var imagesCompleted = new List<long>();
        var articlesCompleted = new List<DownloadArticleArgs>();
        var downloadCompleted = false;

        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        this.ResetArticleDownloader(clearingHouse);

        clearingHouse.DownloadingStarted += (_, _) => downloadStarted = true;
        clearingHouse.ArticleStarted += (_, args) => articlesStarted.Add(args);
        clearingHouse.ImagesStarted += (_, articleId) => imagesStarted.Add(articleId);
        clearingHouse.ImageStarted += (_, uri) => imageStarted.Add(uri);
        clearingHouse.ImageCompleted += (_, uri) => imageCompleted.Add(uri);
        clearingHouse.ImagesCompleted += (_, articleId) => imagesCompleted.Add(articleId);
        clearingHouse.ArticleCompleted += (_, args) => articlesCompleted.Add(args);
        clearingHouse.DownloadingCompleted += (_, _) => downloadCompleted = true;

        await this.articleDownloader.DownloadBookmarks(articlesToDownload);

        Assert.True(downloadStarted);
        Assert.Equal(articleIds.Length, articlesStarted.Count);
        Assert.Equal(articleIdsWithoutMissingArticle, imagesStarted);
        Assert.Equal(imageStarted, imageCompleted);
        Assert.Equal(articleIdsWithoutMissingArticle, imagesCompleted);
        Assert.Equal(articleIds.Length, articlesCompleted.Count);
        Assert.True(downloadCompleted);
    }
    #endregion

    #region Cancellation
    [Fact]
    public async Task NoImagesDownloadedIfCancelledBeforeImagesStarted()
    {
        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        var cancelSource = new CancellationTokenSource();

        this.ResetArticleDownloader(clearingHouse);

        var imagesStarted = false;
        DownloadArticleArgs? articleCompleted = null;
        clearingHouse.ArticleStarted += (_, args) => cancelSource.Cancel();
        clearingHouse.ImagesStarted += (_, articleId) => imagesStarted = true;
        clearingHouse.ArticleCompleted += (_, args) => articleCompleted = args;

        await Assert.ThrowsAsync<OperationCanceledException>(() => articleDownloader.DownloadBookmark(IMAGES_ARTICLE, cancelSource.Token));
        Assert.False(imagesStarted);

        // Guess the article path, since we cancelled early, we don't have local
        // state if things are working correctly.
        var localArticlePath = Path.Join(this.testDirectory.FullName, $"{IMAGES_ARTICLE}.html");
        Assert.False(File.Exists(localArticlePath));

        var imagesPath = Path.Join(this.testDirectory.FullName, IMAGES_ARTICLE.ToString());
        Assert.False(Directory.Exists(imagesPath));

        // Check that the completed args was fired with the correct info
        Assert.Equal(new DownloadArticleArgs(IMAGES_ARTICLE, "Article With Images"), articleCompleted);
    }

    [Fact]
    public async Task NoImagesDownloadedIfCancelledBeforeFirstImageStarted()
    {
        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        var cancelSource = new CancellationTokenSource();

        this.ResetArticleDownloader(clearingHouse);

        var imagesStarted = false;
        DownloadArticleArgs? articleCompleted = null;
        clearingHouse.ImagesStarted += (_, _) => cancelSource.Cancel();
        clearingHouse.ArticleCompleted += (_, args) => articleCompleted = args;

        await Assert.ThrowsAsync<OperationCanceledException>(() => articleDownloader.DownloadBookmark(IMAGES_ARTICLE, cancelSource.Token));
        Assert.False(imagesStarted);

        // Guess the article path, since we cancelled early, we don't have local
        // state if things are working correctly.
        var localArticlePath = Path.Join(this.testDirectory.FullName, $"{IMAGES_ARTICLE}.html");
        Assert.False(File.Exists(localArticlePath));

        var imagesPath = Path.Join(this.testDirectory.FullName, IMAGES_ARTICLE.ToString());
        Assert.False(Directory.Exists(imagesPath));

        // Check that the completed args was fired with the correct info
        Assert.Equal(new DownloadArticleArgs(IMAGES_ARTICLE, "Article With Images"), articleCompleted);
    }

    [Fact]
    public async Task NoMoreImagesDownloadedIfCancelledBeforeSecondImageStarted()
    {
        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        var cancelSource = new CancellationTokenSource();

        this.ResetArticleDownloader(clearingHouse);

        var imagesSeen = 0;
        DownloadArticleArgs? articleCompleted = null;
        clearingHouse.ImageStarted += (_, _) =>
        {
            imagesSeen += 1;

            if (imagesSeen == 2)
            {
                cancelSource.Cancel();
            }
        };

        clearingHouse.ArticleCompleted += (_, args) => articleCompleted = args;

        await Assert.ThrowsAsync<OperationCanceledException>(() => articleDownloader.DownloadBookmark(IMAGES_ARTICLE, cancelSource.Token));
        Assert.Equal(2, imagesSeen);

        // Guess the article path, since we cancelled early, we don't have local
        // state if things are working correctly.
        var localArticlePath = Path.Join(this.testDirectory.FullName, $"{IMAGES_ARTICLE}.html");
        Assert.False(File.Exists(localArticlePath));

        var imagesPath = Path.Join(this.testDirectory.FullName, IMAGES_ARTICLE.ToString());
        if (Directory.Exists(imagesPath))
        {
            var files = Directory.GetFiles(imagesPath);
            Assert.Equal(2, files.Length);
        }

        // Check that the completed args was fired with the correct info
        Assert.Equal(new DownloadArticleArgs(IMAGES_ARTICLE, "Article With Images"), articleCompleted);
    }

    [Fact]
    public async Task NoMoreImagesDownloadedIfCancelledWithinDownloadRequestBeforeSecondImageStarted()
    {
        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        var cancelSource = new CancellationTokenSource();

        this.ResetArticleDownloader(clearingHouse);

        var imagesSeen = 0;
        DownloadArticleArgs? articleCompleted = null;
        this.localFileHttpHandler.FileRequested += (_, _) =>
        {
            imagesSeen += 1;

            if (imagesSeen == 2)
            {
                cancelSource.Cancel();
            }
        };

        clearingHouse.ArticleCompleted += (_, args) => articleCompleted = args;

        await Assert.ThrowsAsync<OperationCanceledException>(() => articleDownloader.DownloadBookmark(IMAGES_ARTICLE, cancelSource.Token));
        Assert.Equal(2, imagesSeen);

        // Guess the article path, since we cancelled early, we don't have local
        // state if things are working correctly.
        var localArticlePath = Path.Join(this.testDirectory.FullName, $"{IMAGES_ARTICLE}.html");
        Assert.False(File.Exists(localArticlePath));

        var imagesPath = Path.Join(this.testDirectory.FullName, IMAGES_ARTICLE.ToString());
        if (Directory.Exists(imagesPath))
        {
            var files = Directory.GetFiles(imagesPath);
            Assert.Equal(2, files.Length);
        }

        // Check that the completed args was fired with the correct info
        Assert.Equal(new DownloadArticleArgs(IMAGES_ARTICLE, "Article With Images"), articleCompleted);
    }
    #endregion

    #region Bulk Downloading
    [Fact]
    public async Task DownloadingWithListOfIdsDownloadsAll()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            IMAGES_WITH_QUERY_STRINGS,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };
        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        await this.articleDownloader.DownloadBookmarks(articlesToDownload);

        var articlesLocalState = articleIds.Select((id) => this.articleDatabase.GetLocalOnlyStateByArticleId(id)).OfType<DatabaseLocalOnlyArticleState>().ToList()!;
        Assert.Equal(articleIds.Length, articlesLocalState.Count);

        Assert.All(articlesLocalState, this.AssertAvailableLocallyAndFileExists);
    }

    [Fact]
    public async Task DownloadingMultipleArticlesWhereOneIsUnavailableDownloadsTheRest()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            UNAVAILABLE_ARTICLE,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };
        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        await this.articleDownloader.DownloadBookmarks(articlesToDownload);

        var articlesLocalState = articleIds.Select((id) => this.articleDatabase.GetLocalOnlyStateByArticleId(id)).OfType<DatabaseLocalOnlyArticleState>().ToList()!;
        Assert.Equal(articleIds.Length, articlesLocalState.Count);

        Assert.All(articlesLocalState, (state) =>
        {
            if (state.ArticleId == UNAVAILABLE_ARTICLE)
            {
                // This article shouldn't be downloaded, and should be inspected
                // differently
                Assert.True(state.ArticleUnavailable);
                Assert.Null(state.LocalPath);
                Assert.False(state.AvailableLocally);
                Assert.Null(state.FirstImageLocalPath);
                Assert.Null(state.FirstImageRemoteUri);
                return;
            }

            this.AssertAvailableLocallyAndFileExists(state);
        });
    }

    [Fact]
    public async Task DownloadingMultipleArticlesWhereOneIsMissingDownloadsTheRest()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            MISSING_REMOTE_ARTICLE,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };
        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        await this.articleDownloader.DownloadBookmarks(articlesToDownload);

        var articlesLocalState = articleIds.Select((id) => this.articleDatabase.GetLocalOnlyStateByArticleId(id)).OfType<DatabaseLocalOnlyArticleState>().ToList()!;
        Assert.Equal(articleIds.Length - 1, articlesLocalState.Count);

        Assert.All(articlesLocalState, this.AssertAvailableLocallyAndFileExists);
    }

    [Fact]
    public async Task CancellingAfterFirstArticleDoesntDownloadAnyMore()
    {
        var cancellationSource = new CancellationTokenSource();
        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        this.ResetArticleDownloader(clearingHouse);

        var articleIds = new long[]
        {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            IMAGES_WITH_QUERY_STRINGS,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };
        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();

        var seenArticles = 0;
        clearingHouse.ArticleStarted += (_, _) =>
        {
            seenArticles += 1;
            if(seenArticles > 2)
            {
                cancellationSource.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.articleDownloader.DownloadBookmarks(articlesToDownload, cancellationSource.Token));

        var articlesLocalState = articleIds.Select((id) => this.articleDatabase.GetLocalOnlyStateByArticleId(id)).OfType<DatabaseLocalOnlyArticleState>().ToList()!;
        Assert.NotEqual(articleIds.Length, articlesLocalState.Count);

        Assert.All(articlesLocalState, this.AssertAvailableLocallyAndFileExists);
    }

    [Fact]
    public async Task CanInitiateDownloadOfAllArticlesMissingLocalState()
    {
        var articlesWithoutLocalStatePreDownload = this.articleDatabase.ListAllArticlesInAFolder().Where((d) => !d.Article.HasLocalState).Select((d) => d.Article);

        await this.articleDownloader.DownloadBookmarksWithoutLocalState();

        var articlesWithoutLocalStatePostDownload = this.articleDatabase.ListAllArticlesInAFolder().Where((d) => !d.Article.HasLocalState).Select((d) => d.Article);
        Assert.Single(articlesWithoutLocalStatePostDownload);
    }

    [Fact]
    public async Task InitiatingDownloadWhenAllArticlesHaveLocalStateNoOps()
    {
        this.articleDatabase.DeleteArticle(MISSING_REMOTE_ARTICLE);
        this.articleDatabase.DeleteArticle(UNAVAILABLE_ARTICLE);
        foreach(var article in this.articleDatabase.ListAllArticlesInAFolder().Where((d) => !d.Article.HasLocalState).Select((d) => d.Article))
        {
            this.articleDatabase.AddLocalOnlyStateForArticle(new DatabaseLocalOnlyArticleState()
            {
                ArticleId = article.Id,
                AvailableLocally = true,
                LocalPath = new Uri(TestUtilities.BASE_URL, $"/{MISSING_REMOTE_ARTICLE}")
            });
        }

        var clearingHouse = new MockArticleDownloaderEventClearingHouse();
        this.ResetArticleDownloader(clearingHouse);

        var downloadsStarted = false;
        clearingHouse.DownloadingStarted += (_,_) => downloadsStarted = true;

        await this.articleDownloader.DownloadBookmarksWithoutLocalState();

        Assert.False(downloadsStarted, "With all local state being present, downloads shouldn't have started");
    }

    [Fact]
    public async Task TransactionCanRollbackMultipleDownloads()
    {
        var articleIds = new long[] {
            BASIC_ARTICLE_NO_IMAGES,
            LARGE_ARTICLE_NO_IMAGES,
            IMAGES_ARTICLE,
            IMAGES_WITH_QUERY_STRINGS,
            IMAGES_WITH_INLINE_IMAGES,
            YOUTUBE_ARTICLE,
            FIRST_IMAGE_JPG
        };
        var articlesToDownload = articleIds.Select((id) => articleDatabase.GetArticleById(id)!).ToList();


        using (var transaction = connection.BeginTransaction())
        {
            await this.articleDownloader.DownloadBookmarks(articlesToDownload);

            transaction.Rollback();
        }

        var articlesLocalState = articleIds.Select((id) => this.articleDatabase.GetLocalOnlyStateByArticleId(id)).OfType<DatabaseLocalOnlyArticleState>().ToList()!;
        Assert.Empty(articlesLocalState);
    }
    #endregion
}

/// <summary>
/// Sample test that adds the known URLs to the service, and then downloads the
/// body of those saved bookmarks. Only needs to be run if the set of bookmarks
/// or service response changes for those bookmarks
/// </summary>
public class SampleDataDownloadingHelper
{
    private static readonly Uri SAMPLE_BASE_URI = new Uri("https://www.codevoid.net/storyvoidtest/");

    [Fact]
    public async Task AddSampleArticlesAndGetTextOnThemToSaveLocally()
    {
        DirectoryInfo? outputDirectory = Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, "TestPageOutput"));

        var bookmarksClient = new BookmarksClient(Codevoid.Test.Instapaper.TestUtilities.GetClientInformation());
        var samplePages = new List<string>()
        {
            "ArticleWithAnimatedGIFFirstImage.html",
            "ArticleWithAnimatedPNGFirstImage.html",
            "ArticleWithFirstImageLessThan150px.html",
            "ArticleWithImages.html",
            "ArticleWithImagesAlt.html", // Instapaper caches content forever, so edits require a new url
            "ArticleWithImagesAndQueryStrings.html",
            "ArticleWithInlineDataImages.html",
            "ArticleWithJPGFirstImage.html",
            "ArticleWithPNGFirstImage.html",
            "ArticleWithStaticGIFFirstImage.html",
            "ArticleWithSVGFirstImage.html",
            "ArticleWithWebPFirstImage.html",
            "BasicArticleNoImage.html",
            "EmptyArticle.html",
            "LargeArticleNoImage.html",
            "ShortArticleNoImage.html"
        };

        foreach (var fileName in samplePages)
        {
            var bookmarkUri = new Uri(SAMPLE_BASE_URI, fileName);
            var bookmarkInfo = await bookmarksClient.AddAsync(bookmarkUri);
            try
            {
                var bookmarkBody = await bookmarksClient.GetTextAsync(bookmarkInfo.Id);
                var outputFile = Path.Join(outputDirectory.FullName, fileName);
                File.WriteAllText(outputFile, bookmarkBody);
            }
            catch (BookmarkContentsUnavailableException)
            {
                Assert.False(true, $"Couldn't download: {bookmarkUri.ToString()}");
            }
        }

        // Vimeo
        var vimeoUri = new Uri("https://vimeo.com/76979871");
        var vimeoInfo = await bookmarksClient.AddAsync(vimeoUri);
        var vimeoBody = await bookmarksClient.GetTextAsync(vimeoInfo.Id);
        File.WriteAllText(Path.Join(outputDirectory.Name, "vimeo.html"), vimeoBody);

        // YouTube
        var youtubeUri = new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var youtubeInfo = await bookmarksClient.AddAsync(youtubeUri);
        var youtubeBody = await bookmarksClient.GetTextAsync(youtubeInfo.Id);
        File.WriteAllText(Path.Join(outputDirectory.Name, "youtube.html"), youtubeBody);
    }
}