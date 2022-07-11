using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class ArticleSyncTests : BaseSyncTest
{
    #region Local-Only Article Pending Changes
    [Fact]
    public async Task PendingArticleAddIsAddedToTheService()
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

    #region Deletes
    [Fact]
    public async Task PendingArticleDeletesInUnreadFolderAreRemovedFromTheService()
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
    public async Task PendingArticleDeletesInUnreadFolderAreCompletedEvenIfArticleMissingOnService()
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
    public async Task PendingArticleDeletesInCustomFolderAreRemovedFromTheService()
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

    #region Moves
    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolder()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButArticleMissingOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // remove the article from the service
        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButFolderIsMissingOnTheService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // remove the folder we moved it to from the service
        var remoteFirstFolder = this.service.MockFolderService.FolderDB.GetFolderByServiceId(firstFolder.ServiceId!.Value)!;
        this.service.MockFolderService.FolderDB.DeleteFolder(remoteFirstFolder.LocalId);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnread()
    {
        // Delete a known article
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticleInCustomFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId).First()!;

        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticleInCustomFolder.Id, WellKnownLocalFolderIds.Unread);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the unread folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArticleInCustomFolder, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnreadButArticleMissingCompletesWithArticleInUnreadFolder()
    {
        // Delete a known article
        var firstFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;
        var firstArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId).First()!;
        
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is not in a folder, for later clean up
        var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
        Assert.Empty(orphanedArticles);

        var unreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(unreadArticles, (a) => a.Url == firstArticle.Url);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadToArchiveHandledAsAnArchive()
    {
        // Delete a known article
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the archive folder folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadToArchiveButArticleMissingCompletes()
    {
        // Delete a known article
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is not in a folder, for later clean up
        var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
        Assert.Contains(firstUnreadArticle, orphanedArticles);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromArchiveToUnreadHandledAsAMove()
    {
        var firstArchiveArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).First()!;

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArchiveArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArchiveArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomToArchiveHandledAsAnArchive()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId).First()!;

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(firstArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromArchiveGoesSomewhereSensible()
    {
        var firstArchivedArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).First()!;
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArchivedArticle.Id, firstFolder.LocalId);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstArchivedArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    
    [Fact]
    public async Task PendingArticleMoveToUnsyncedFolderForceSyncsTheIndividualFolder()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First()!;
        DatabaseFolder? unsyncedNewFolder = null;

        // Move article to a brand new, sync'd folder
        using (var ledger = this.GetLedger())
        {
            unsyncedNewFolder = this.databases.FolderDB.CreateFolder("A new Folder");
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, unsyncedNewFolder.LocalId);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check article still on service
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.NotNull(serviceArticle);

        // Get the new folder from the service
        var serviceFolder = this.service.MockFolderService.FolderDB.GetFolderByTitle(unsyncedNewFolder.Title);
        Assert.NotNull(serviceFolder);

        // Check the article is actually in the folder now
        var serviceFolderContents = this.service.MockBookmarksService.ArticleDB.ListArticlesForLocalFolder(serviceFolder!.LocalId);
        Assert.Contains(serviceArticle, serviceFolderContents);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #region Liking
    [Fact]
    public async Task PendingArticleLikeSyncsToService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeButAlreadyLikedOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;
        _ = this.service.MockBookmarksService.ArticleDB.LikeArticle(firstUnreadArticle.Id);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeMissingArticleDoesntAddArticle()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;
        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(serviceArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeSyncsToService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;

        // Make sure the article starts out liked
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        this.service.MockBookmarksService.ArticleDB.LikeArticle(firstUnreadArticle.Id);

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is unliked
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeButAlreadyUnlikedOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id); // Make sure its liked so we generate a pending edit
        

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is unliked
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeMissingArticleDoesntAddArticle()
    {
        var firstUnreadArticle = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => !a.Liked)!;
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id); // Make sure its liked so we generate a pending edit

        this.service.MockBookmarksService.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is not re-added
        var serviceArticle = this.service.MockBookmarksService.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(serviceArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #endregion
}