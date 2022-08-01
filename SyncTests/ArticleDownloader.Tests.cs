using System.Data;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Codevoid.Test.Storyvoid;

public class ArticleDownloaderTests : IDisposable
{
    private readonly IDbConnection connection;
    private readonly IArticleDatabase articleDatabase;
    private readonly ArticleDownloader articleDownloader;
    private readonly IBookmarksClient bookmarkClient;

    private readonly DirectoryInfo testDirectory;
    private readonly string sampleFilesFolder = Path.Join(Environment.CurrentDirectory, "mockbookmarkresponses");

    #region Mock Article IDs
    private const long MISSING_ARTICLE = 9L;
    private const long BASIC_ARTICLE_NO_IMAGES = 10L;
    private const long UNAVAILABLE_ARTICLE = 11L;
    private const long LARGE_ARTICLE_NO_IMAGES = 12L;
    private const long SHORT_ARTICLE_NO_IMAGES = 14L;
    private const long EMPTY_ARTICLE = 15L;
    private const long YOUTUBE_ARTICLE = 16L;
    private const long IMAGES_ARTICLE = 17L;
    private const long IMAGES_WITH_QUERY_STRINGS = 18L;
    private const long IMAGES_WITH_INLINE_IMAGES = 19L;
    #endregion

    public ArticleDownloaderTests()
    {
        var testFolderId = Guid.NewGuid().ToString();
        this.testDirectory = Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, testFolderId));

        var (connection, _, _, articleDatabase, _) = TestUtilities.GetEmptyDatabase();
        this.connection = connection;
        this.articleDatabase = articleDatabase;

        var fileMap = PopulateDownloadableArticles();
        this.bookmarkClient = new MockBookmarkServiceWithOnlyGetText(fileMap);
        this.articleDownloader = new ArticleDownloader(
            this.testDirectory.FullName,
            this.articleDatabase,
            this.bookmarkClient,
            Test.Instapaper.TestUtilities.GetClientInformation()
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
            
            articleFileMap.Add(id, (!String.IsNullOrWhiteSpace(filename) ? Path.Join(this.sampleFilesFolder, filename) : filename));
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
    }

    #region Basic Downloading
    [Fact]
    public async Task CanDownloadArticleWithoutImages()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState.ArticleId), localState.LocalPath);
        Assert.True(localState.AvailableLocally);
        Assert.False(localState.ArticleUnavailable);

        var fileExists = File.Exists(Path.Join(this.testDirectory.FullName, localState.LocalPath!.PathAndQuery));
        Assert.True(fileExists);
    }

    [Fact]
    public async Task LocalFileContainsOnlyTheBody()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState.ArticleId), localState.LocalPath);
        Assert.True(localState.AvailableLocally);
        Assert.False(localState.ArticleUnavailable);

        var localContents = File.ReadAllText(Path.Join(this.testDirectory.FullName, localState.LocalPath!.PathAndQuery));

        // We want to make sure that we don't turn this into a fully fledges HTML
        // document, as the consumer is expected to perform the appropriate
        // cleanup
        Assert.StartsWith("<body>", localContents);
        Assert.EndsWith( "</body>", localContents);
    }

    [Fact]
    public async Task CanDownloadLargeArticleWithoutImages()
    {
        var localState = await this.articleDownloader.DownloadBookmark(LARGE_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState.ArticleId), localState.LocalPath);
        Assert.True(localState.AvailableLocally);
        Assert.False(localState.ArticleUnavailable);

        var fileExists = File.Exists(Path.Join(this.testDirectory.FullName, localState.LocalPath!.PathAndQuery));
        Assert.True(fileExists);
    }

    [Fact]
    public async Task DownloadingUnavailableArticleMarksArticleAsUnavailable()
    {
        var localState = await this.articleDownloader.DownloadBookmark(UNAVAILABLE_ARTICLE);
        Assert.Null(localState.LocalPath);
        Assert.False(localState.AvailableLocally);
        Assert.True(localState.ArticleUnavailable);
    }

    [Fact]
    public async Task DownloadingArticleThatIsntOnTheServiceFails()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => this.articleDownloader.DownloadBookmark(MISSING_ARTICLE));
        var localState = this.articleDatabase.GetLocalOnlyStateByArticleId(MISSING_ARTICLE);
        Assert.Null(localState);
    }

    [Fact]
    public async Task ArticleTextIsExtractedFromBodyText()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.NotEmpty(localState.ExtractedDescription);
        Assert.Equal(400, localState.ExtractedDescription.Length);
    }

    [Fact]
    public async Task ArticleTextIsExtractedFromBodyTextWhenShort()
    {
        var localState = await this.articleDownloader.DownloadBookmark(SHORT_ARTICLE_NO_IMAGES);
        Assert.NotEmpty(localState.ExtractedDescription);
        Assert.Equal(15, localState.ExtractedDescription.Length);
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
        Assert.Empty(localState.ExtractedDescription);
    }
    #endregion

    #region Image Processing
    [Fact]
    public async Task CanDownloadArticleWithImages()
    {
        const int EXPECTED_IMAGE_COUNT = 9;
        var localState = await this.articleDownloader.DownloadBookmark(IMAGES_ARTICLE);
        var imagesPath = Path.Join(this.testDirectory.FullName, localState.ArticleId.ToString());
        Assert.True(Directory.Exists(imagesPath));
        Assert.Equal(EXPECTED_IMAGE_COUNT, Directory.GetFiles(imagesPath).Count());

        var localPath = Path.Join(this.testDirectory.FullName, localState.LocalPath!.AbsolutePath);
        var htmlParserContext = BrowsingContext.New(ArticleDownloader.ParserConfiguration);
        var document = await htmlParserContext.OpenAsync((r) => r.Content(File.Open(localPath, FileMode.Open, FileAccess.Read), true));

        var seenImages = 0;
        foreach(var image in document.QuerySelectorAll<IHtmlImageElement>("img[src]"))
        {
            var imageSrc = image.GetAttribute("src")!;
            if(imageSrc!.StartsWith("http"))
            {
                // We only look for processed images
                continue;
            }

            seenImages += 1;

            var imagePath = Path.Combine(this.testDirectory.FullName, imageSrc);
            Assert.True(File.Exists(imagePath));
        }

        Assert.Equal(EXPECTED_IMAGE_COUNT, seenImages);
    }

    [Fact]
    public async Task CanDownloadArticleWithImagesThatHaveQueryStringsInTheirUrls()
    {
        const int EXPECTED_IMAGE_COUNT = 9;
        var localState = await this.articleDownloader.DownloadBookmark(IMAGES_WITH_QUERY_STRINGS);
        var imagesPath = Path.Join(this.testDirectory.FullName, localState.ArticleId.ToString());
        Assert.True(Directory.Exists(imagesPath));
        Assert.Equal(EXPECTED_IMAGE_COUNT, Directory.GetFiles(imagesPath).Count());

        var localPath = Path.Join(this.testDirectory.FullName, localState.LocalPath!.AbsolutePath);
        var htmlParserContext = BrowsingContext.New(ArticleDownloader.ParserConfiguration);
        var document = await htmlParserContext.OpenAsync((r) => r.Content(File.Open(localPath, FileMode.Open, FileAccess.Read), true));

        var seenImages = 0;
        foreach(var image in document.QuerySelectorAll<IHtmlImageElement>("img[src]"))
        {
            var imageSrc = image.GetAttribute("src")!;
            if(imageSrc!.StartsWith("http"))
            {
                // We only look for processed images
                continue;
            }

            seenImages += 1;

            var imagePath = Path.Combine(this.testDirectory.FullName, imageSrc);
            Assert.True(File.Exists(imagePath));
        }

        Assert.Equal(EXPECTED_IMAGE_COUNT, seenImages);
    }

    [Fact]
    public async Task CanDownloadArticleWithImagesWithInlineData()
    {
        const int EXPECTED_IMAGE_COUNT = 2;
        var localState = await this.articleDownloader.DownloadBookmark(IMAGES_WITH_INLINE_IMAGES);
        var imagesPath = Path.Join(this.testDirectory.FullName, localState.ArticleId.ToString());
        Assert.False(Directory.Exists(imagesPath));

        var localPath = Path.Join(this.testDirectory.FullName, localState.LocalPath!.AbsolutePath);
        var htmlParserContext = BrowsingContext.New(ArticleDownloader.ParserConfiguration);
        var document = await htmlParserContext.OpenAsync((r) => r.Content(File.Open(localPath, FileMode.Open, FileAccess.Read), true));

        var seenImages = 0;
        foreach(var image in document.QuerySelectorAll<IHtmlImageElement>("img[src]"))
        {
            var imageSrc = image.GetAttribute("src")!;
            Assert.StartsWith("data:", imageSrc);

            seenImages += 1;
        }

        Assert.Equal(EXPECTED_IMAGE_COUNT, seenImages);
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
            } catch(BookmarkContentsUnavailableException) {
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