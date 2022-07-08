using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class ArticleSyncTests : BaseSyncTest
{
    #region Local-Only Article Pending Changes
    [Fact]
    public async void PendingArticleAddIsAddedToTheService()
    {
        this.SwitchToEmptyLocalDatabase();
        this.SwitchToEmptyServiceDatabase();

        // Add an article directly to pending adds, since we don't have a way to
        // add a 'complete' article explicitly ('cause the service needs to do
        // much of the work)
        var addedUrl = TestUtilities.GetRandomUrl();
        this.databases.ArticleChangesDB.CreatePendingArticleAdd(addedUrl, null);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Make sure we can see it on the service
        var serviceArticles = this.service.MockBookmarksService.ArticleDB.ListAllArticlesInAFolder();
        Assert.Single(serviceArticles);
        Assert.Equal(addedUrl, serviceArticles[0]!.Article.Url);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async void PendingArticleDeletesInUnreadFolderAreRemovedFromTheService()
    {
        // Delete a known article
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articleFromService = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async void PendingArticleDeletesInUnreadFolderAreCompletedEvenIfArticleMissingOnService()
    {
        // Delete a known article
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Delete it from the service mock
        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's really gone from the service
        var articleFromService = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);

        // Make suer pending edits are cleaned up
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async void PendingArticleDeletesInCustomFolderAreRemovedFromTheService()
    {
        // Folder we'll add to
        var firstUserFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;

        // Delete a known article from that folder
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(firstUserFolder.LocalId).First()!;
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articleFromService = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion
}