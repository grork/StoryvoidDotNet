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
    private readonly string sampleFilesFolder = Path.Join(Environment.CurrentDirectory, "samplearticles");

    #region Mock Article IDs
    private const long BASIC_ARTICLE_NO_IMAGES = 10L;
    #endregion

    private static ArticleRecordInformation GetInfoFor(long id, string title)
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

        // Article without images
        var basicArticleNoImages = GetInfoFor(BASIC_ARTICLE_NO_IMAGES, "Basic Article Without Images");
        this.articleDatabase.AddArticleToFolder(basicArticleNoImages, WellKnownLocalFolderIds.Unread);
        articleFileMap.Add(basicArticleNoImages.id, Path.Join(this.sampleFilesFolder, "BasicArticleNoImages.html"));

        return articleFileMap;
    }

    private Uri GetRelativeUriForDownloadedArticle(long articleId)
    {
        return new Uri(ArticleDownloader.ROOT_URI, Path.Join(articleId.ToString(), $"{articleId}.html"));
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
    }
}