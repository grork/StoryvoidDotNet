using System.Data;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid;

public class ArticleDownloaderTests : IDisposable
{
    private readonly IDbConnection connection;
    private readonly IArticleDatabase articleDatabase;
    private readonly ArticleDownloader articleDownloader;

    private readonly DirectoryInfo testDirectory;
    private readonly string sampleFilesFolder = Path.Join(Environment.CurrentDirectory, "mockbookmarkresponses");

    #region Mock Article IDs
    private const long MISSING_ARTICLE = 9L;
    private const long BASIC_ARTICLE_NO_IMAGES = 10L;
    private const long UNAVAILABLE_ARTICLE = 11L;
    #endregion

    private static ArticleRecordInformation GetRecordInfoFor(long id, string title)
    {
        return new(
            id: id,
            title: title,
            url: new Uri(TestUtilities.BASE_URL, $"/{id}"),
            description: String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "ABC",
            liked: false
        );
    }

    public ArticleDownloaderTests()
    {
        var testFolderId = Guid.NewGuid().ToString();
        this.testDirectory = Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, testFolderId));

        var (connection, _, _, articleDatabase, _) = TestUtilities.GetEmptyDatabase();
        this.connection = connection;
        this.articleDatabase = articleDatabase;

        var fileMap = PopulateDownloadableArticles();
        this.articleDownloader = new ArticleDownloader(
            this.testDirectory.FullName,
            this.articleDatabase,
            new MockBookmarkServiceWithOnlyGetText(fileMap)
        );
    }

    private IDictionary<long, string> PopulateDownloadableArticles()
    {
        IDictionary<long, string> articleFileMap = new Dictionary<long, string>();

        // Unavailable article
        var unavailableArticle = GetRecordInfoFor(UNAVAILABLE_ARTICLE, "Article with no content available");
        this.articleDatabase.AddArticleToFolder(unavailableArticle, WellKnownLocalFolderIds.Unread);
        articleFileMap.Add(UNAVAILABLE_ARTICLE, String.Empty);

        // Article without images
        var basicArticleNoImages = GetRecordInfoFor(BASIC_ARTICLE_NO_IMAGES, "Basic Article Without Images");
        this.articleDatabase.AddArticleToFolder(basicArticleNoImages, WellKnownLocalFolderIds.Unread);
        articleFileMap.Add(basicArticleNoImages.id, Path.Join(this.sampleFilesFolder, "BasicArticleNoImage.html"));

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

    [Fact]
    public async Task CanDownloadArticleWithoutImages()
    {
        var localState = await this.articleDownloader.DownloadBookmark(BASIC_ARTICLE_NO_IMAGES);
        Assert.Equal(this.GetRelativeUriForDownloadedArticle(localState.ArticleId), localState.LocalPath);
        Assert.True(localState.AvailableLocally);
        Assert.False(localState.ArticleUnavailable);
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
}

/// <summary>
/// Sample test that adds the known URLs to the service, and then downloads the
/// body of those saved bookmarks. Only needs to be run if the set of bookmarks
/// or service response changes for those bookmarks
/// </summary>
public class SampleDataDownloadingHelper
{
    private static readonly Uri SAMPLE_BASE_URI = new Uri("https://www.codevoid.net/storyvoidtest/");

    [Fact(Skip = "Not a real test; intended to facilitate downloading sample data")]
    public async Task AddSampleArticlesAndGetTextOnThemToSaveLocally()
    {
        DirectoryInfo? outputDirectory = Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, "TestPageOutput"));

        var bookmarksClient = new BookmarksClient(Codevoid.Test.Instapaper.TestUtilities.GetClientInformation());
        var samplePages = new List<string>()
        {
            "BasicArticleNoImage.html",
            "LargeArticleNoImage.html",
            "ArticleWithImages.html",
            "ArticleWithInlineDataImages.html",
            "ArticleWithFirstImageLessThan150px.html",
            "ArticleWithAnimatedGIFFirstImage.html",
            "ArticleWithAnimatedPNGFirstImage.html",
            "ArticleWithJPGFirstImage.html",
            "ArticleWithPNGFirstImage.html",
            "ArticleWithStaticGIFFirstImage.html",
            "ArticleWithSVGFirstImage.html",
            "ArticleWithWebPFirstImage.html"
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