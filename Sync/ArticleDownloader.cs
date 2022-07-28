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
        var body = await this.bookmarksClient.GetTextAsync(bookmarkId);
        var bookmarkDirectory = Directory.CreateDirectory(Path.Combine(workingRoot, bookmarkId.ToString()));
        var bookmarkRelativePath = Path.Combine(bookmarkDirectory.Name, $"{bookmarkId}.html");
        var bookmarkAbsoluteFilePath = Path.Combine(workingRoot, bookmarkRelativePath);
        File.WriteAllText(bookmarkAbsoluteFilePath, body, Encoding.UTF8);

        return articleDatabase.AddLocalOnlyStateForArticle(new DatabaseLocalOnlyArticleState()
        {
            ArticleId = bookmarkId,
            AvailableLocally = true,
            LocalPath = new Uri(ROOT_URI, bookmarkRelativePath),
        });
    }
}