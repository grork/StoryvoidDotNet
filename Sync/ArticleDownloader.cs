using System.Text;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;

namespace Codevoid.Storyvoid.Sync;

public class ArticleDownloader
{
    public static readonly Uri ROOT_URI = new Uri("localfile://local");

    private string workingRoot;
    private IArticleDatabase articleDatabase;
    private IBookmarksClient bookmarksClient;

    public ArticleDownloader(string workingRoot,
                             IArticleDatabase articleDatabase,
                             IBookmarksClient bookmarksClient)
    {
        this.workingRoot = workingRoot;
        this.articleDatabase = articleDatabase;
        this.bookmarksClient = bookmarksClient;
    }

    public async Task<DatabaseLocalOnlyArticleState> DownloadBookmark(long bookmarkId)
    {
        var bookmarkFileName = $"{bookmarkId}.html";
        var bookmarkAbsoluteFilePath = Path.Combine(workingRoot, bookmarkFileName);
        var articleDownloaded = true;
        var contentsUnavailable = false;
        Uri? localPath = null;

        try
        {
            var body = await this.bookmarksClient.GetTextAsync(bookmarkId);
            File.WriteAllText(bookmarkAbsoluteFilePath, body, Encoding.UTF8);
            articleDownloaded = true;
            localPath = new Uri(ROOT_URI, bookmarkFileName);
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
        });
    }
}