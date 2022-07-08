namespace Codevoid.Test.Storyvoid;

public class ArticleSyncTests : BaseSyncTest
{

    [Fact]
    public async void PendingArticleAddIsAddedToTheService()
    {
        this.SwitchToEmptyLocalDatabase();
        this.SwitchToEmptyServiceDatabase();

        var addedUrl = TestUtilities.GetRandomUrl();

        this.databases.ArticleChangesDB.CreatePendingArticleAdd(addedUrl, null);

        await this.syncEngine.SyncBookmarks();

        var serviceArticles = this.service.MockBookmarksService.ArticleDB.ListAllArticlesInAFolder();
        Assert.Single(serviceArticles);
        Assert.Equal(addedUrl, serviceArticles[0]!.Article.Url);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
}