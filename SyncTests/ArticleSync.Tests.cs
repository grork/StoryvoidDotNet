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
        var serviceArticles = this.service.BookmarksClient.ArticleDB.ListAllArticlesInAFolder();
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
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articleFromService = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleDeletesInUnreadFolderAreCompletedEvenIfArticleMissingOnService()
    {
        // Delete a known article
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Delete it from the service mock
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's really gone from the service
        var articleFromService = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);

        // Make suer pending edits are cleaned up
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleDeletesInCustomFolderAreRemovedFromTheService()
    {
        // Folder we'll add to
        var firstUserFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Delete a known article from that folder
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstUserFolder.LocalId);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articleFromService = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(articleFromService);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #region Moves
    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolder()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButArticleMissingOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // remove the article from the service
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's gone
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButFolderIsMissingOnTheService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // remove the folder we moved it to from the service
        var remoteFirstFolder = this.service.FoldersClient.FolderDB.GetFolderByServiceId(firstFolder.ServiceId!.Value)!;
        this.service.FoldersClient.FolderDB.DeleteFolder(remoteFirstFolder.LocalId);

        // Sync
        await this.syncEngine.SyncPendingBookmarkMoves();

        // Check it's gone
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnread()
    {
        // Delete a known article
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticleInCustomFolder = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticleInCustomFolder.Id, WellKnownLocalFolderIds.Unread);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the unread folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArticleInCustomFolder, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnreadButArticleMissingCompletesWithArticleInUnreadFolderLocally()
    {
        // Select an article to move
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);
        
        // Move it
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Delete it from the service, so it will fail when we try to apply the
        // move on the service.
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstArticle.Id);

        // Sync
        await this.syncEngine.SyncPendingBookmarkMoves();

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
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the archive folder folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(firstUnreadArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadToArchiveButArticleMissingCompletes()
    {
        // Move an article to a folder...
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        
        using(var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // ... delete that article
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

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
        var firstArchiveArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArchiveArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArchiveArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomToArchiveHandledAsAnArchive()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(firstArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromArchiveGoesSomewhereSensible()
    {
        var firstArchivedArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArchivedArticle.Id, firstFolder.LocalId);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstArchivedArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    
    [Fact]
    public async Task PendingArticleMoveToUnsyncedFolderForceSyncsTheIndividualFolder()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
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
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.NotNull(serviceArticle);

        // Get the new folder from the service
        var serviceFolder = this.service.FoldersClient.FolderDB.GetFolderByTitle(unsyncedNewFolder.Title);
        Assert.NotNull(serviceFolder);

        // Check the article is actually in the folder now
        var serviceFolderContents = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFolder!.LocalId);
        Assert.Contains(serviceArticle, serviceFolderContents);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    
    [Fact]
    public async Task PendingArticleMoveFromUnreadSyncingAlsoAppliesServiceArticlePropertyChanges()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // Make additional changes to that article on the service
        var updatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(updatedArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(updatedArticle, firstUnreadArticle);
    }
    
    [Fact]
    public async Task PendingArticleMoveToUnreadSyncingAlsoAppliesServiceArticlePropertyChanges()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move article to unread folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Make additional changes to that article on the service
        var updatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(updatedArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(updatedArticle, firstUnreadArticle);
    }

    [Fact]
    public async Task PendingArticleMoveToArchiveSyncingAlsoAppliesServiceArticlePropertyChanges()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move article to unread folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Make additional changes to that article on the service
        var updatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check it's in the new folder
        var articlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(updatedArticle, articlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(updatedArticle, firstUnreadArticle);
    }
    #endregion

    #region Liking
    [Fact]
    public async Task PendingArticleLikeSyncsToService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeButAlreadyLikedOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        _ = this.service.BookmarksClient.ArticleDB.LikeArticle(firstUnreadArticle.Id);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeMissingArticleDoesntAddArticle()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is liked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(serviceArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeSyncsToService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        // Make sure the article starts out liked
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        this.service.BookmarksClient.ArticleDB.LikeArticle(firstUnreadArticle.Id);

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is unliked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeButAlreadyUnlikedOnService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id); // Make sure its liked so we generate a pending edit
        

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is unliked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeMissingArticleDoesntAddArticle()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id); // Make sure its liked so we generate a pending edit

        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is not re-added
        var serviceArticle = this.service.BookmarksClient.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(serviceArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeSyncPicksUpOtherPropertyChangesFromService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Make additional changes to that article on the service
        var updatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Hash = DateTime.Now.ToString(),
        }));

        // Sync
        await this.syncEngine.SyncBookmarkLikeStatuses();

        // Check the article is liked
        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(serviceArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        
        // Update the reference article to be liked. We don't want to get it
        // from the service again incase state got stomped, and this preserves
        // the actual expectation
        updatedArticle = updatedArticle with
        {
            Liked = true
        };
        
        Assert.Equal(updatedArticle, firstUnreadArticle);
    }
    #endregion

    #endregion

    #region Service-Only Article Changes
    [Fact]
    public async Task ServiceArticleAddedToUnreadIsAddedLocally()
    {
        var addedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb();

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(addedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(addedArticle, localArticle);

        // Check that service & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        var serviceUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(serviceUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleDeletedFromUnreadIsRemovedLocally()
    {
        var firstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        if(localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }        

        // Check that service & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        var serviceUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(serviceUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleAddedToArchiveIsAddedLocally()
    {
        var addedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb(WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(addedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(addedArticle, localArticle);

        // Check that service & local agree on unread contents
        var localArchive = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        var serviceArchive = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(serviceArchive, localArchive);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleDeletedFromArchiveIsRemovedLocally()
    {
        var firstArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstArchiveArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id);
        if(localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }        

        // Check that service & local agree on unread contents
        var localArchive = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        var serviceArchive = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(serviceArchive, localArchive);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleAddedToUserFolderIsAddedLocally()
    {
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;
        var addedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb(serviceFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(addedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(addedArticle, localArticle);

        // Check that service & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        var serviceUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(serviceUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleDeletedFromUserFolderIsRemovedLocally()
    {
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;
        var serviceArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(serviceFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(serviceArticle.Id);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceArticle.Id);
        if(localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }        

        // Check that service & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromUnreadToUserFolderIsMovedLocally()
    {
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;
        var serviceUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceUnreadArticle.Id, serviceFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceUnreadArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceUnreadArticle, localArticle);

        // Check that service & local agree on custom folder contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromUnreadToArchiveIsMovedLocally()
    {
        var serviceUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceUnreadArticle.Id, WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceUnreadArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceUnreadArticle, localArticle);

        // Check that service & local agree on Archive contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromUserFolderToUnreadIsMovedLocally()
    {
        // Select a folder to move from
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to unread
        var serviceUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(serviceFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceUserArticle.Id, WellKnownLocalFolderIds.Unread);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceUserArticle, localArticle);

        // Check that service & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromUserFolderToArchiveIsMovedLocally()
    {
        // Select a folder to move from
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to archive
        var serviceUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(serviceFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceUserArticle.Id, WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceUserArticle, localArticle);

        // Check that service & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromUserFolderToUserFolderIsMovedLocally()
    {
        // Select a folder to move from
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;

        // Select a folder to move to
        var serviceSecondUserFolder = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId != serviceFirstUserFolder.ServiceId);
        var localSecondUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceSecondUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to the second folder
        var serviceUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(serviceFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceUserArticle.Id, serviceSecondUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceUserArticle, localArticle);

        // Check that service & local agree on second folder contents contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localSecondUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceSecondUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromArchiveToUserFolderIsMovedLocally()
    {
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;
        var serviceArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceArchiveArticle.Id, serviceFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceArchiveArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceArchiveArticle, localArticle);

        // Check that service & local agree on custom folder contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task ServiceArticleMovedFromArchiveToUnreadIsMovedLocally()
    {
        var serviceArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceArchiveArticle.Id, WellKnownLocalFolderIds.Unread);

        // Sync
        await this.syncEngine.SyncBookmarks();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(serviceArchiveArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(serviceArchiveArticle, localArticle);

        // Check that service & local agree on Archive contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);
        
        var serviceFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(serviceFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #region List API-based property updates
    [Fact]
    public async Task LocalProgressChangeInUnreadNewerThanServiceIsAppliedToService()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var newProgress = firstUnreadArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now;
        firstUnreadArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.Equal(newProgress, firstUnreadArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstUnreadArticle.ReadProgressTimestamp);
        Assert.Equal(firstUnreadArticle, serviceArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUnreadOlderThanServiceIsAppliedLocally()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var newProgress = firstUnreadArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(10));
        firstUnreadArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.NotEqual(newProgress, firstUnreadArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstUnreadArticle.ReadProgressTimestamp);
        Assert.Equal(firstUnreadArticle, serviceArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInArchiveNewerThanServiceIsAppliedToService()
    {
        var firstArchiveArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        var newProgress = firstArchiveArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now;
        firstArchiveArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstArchiveArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.Equal(newProgress, firstArchiveArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, serviceArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInArchiveOlderThanServiceIsAppliedLocally()
    {
        var firstArchiveArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        var newProgress = firstArchiveArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(10));
        firstArchiveArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstArchiveArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.NotEqual(newProgress, firstArchiveArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, serviceArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUserFolderNewerThanServiceIsAppliedToService()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArchiveArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);
        var newProgress = firstArchiveArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now;
        firstArchiveArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstArchiveArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.Equal(newProgress, firstArchiveArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, serviceArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUserFolderOlderThanServiceIsAppliedLocally()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArchiveArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);
        var newProgress = firstArchiveArticle.ReadProgress + 0.5F;
        var newProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(10));
        firstArchiveArticle = this.databases.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstArchiveArticle with
        {
            ReadProgress = newProgress,
            ReadProgressTimestamp = newProgressTimestamp,
            Hash = "NEWHASH"
        }));

        await this.syncEngine.SyncBookmarks();

        var serviceArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.NotEqual(newProgress, firstArchiveArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, serviceArticle);
    }

    [Fact]
    public async Task LikedServiceArticleIsLikedLocallyAfterSync()
    {
        var serviceFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        serviceFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(serviceFirstUnreadArticle.Id);

        await this.syncEngine.SyncBookmarks();

        var localArticle = this.databases.ArticleDB.GetArticleById(serviceFirstUnreadArticle.Id)!;
        Assert.True(localArticle.Liked);
        Assert.Equal(serviceFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(localArticle, likedArticles);
    }

    [Fact]
    public async Task UnlikedServiceArticleIsThatIsLikedLocallyBecomesUnlikedAfterSync()
    {
        var localFirstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.LikeArticle(localFirstUnreadArticle.Id);

        await this.syncEngine.SyncBookmarks();

        var localArticle = this.databases.ArticleDB.GetArticleById(localFirstUnreadArticle.Id)!;
        Assert.False(localArticle.Liked);
        Assert.Equal(localFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.DoesNotContain(localArticle, likedArticles);
    }

    [Fact]
    public async Task SyncingWithAServiceDeletedFolderCompletes()
    {
        // Makes sure liked articles don't confuse this test. This test expects
        // the article state to remain the same, but because *like* syncing will
        // see the article is no longer liked -- but the general sync due to the
        // missing folder won't delete it -- and unlike it. and thus we have
        // in consistent state. In a more realistic scenario, the deleted folder
        // would have been cleaned up, and the articles would be deleted. But,
        // we're testing a pathological case, so we'll make this exception.
        this.UnlikeEverything();

        // Get the service folder to delete, and all it's articles.
        var serviceUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var serviceArticles = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceUserFolder.LocalId);

        foreach(var serviceArticle in this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceUserFolder.LocalId))
        {
            this.service.BookmarksClient.ArticleDB.DeleteArticle(serviceArticle.Id);
        }

        var localFolderThatWasDeletedContents =
            this.databases.ArticleDB.ListArticlesForLocalFolder(
                this.databases.FolderDB.GetFolderByServiceId(serviceUserFolder.ServiceId!.Value)!.LocalId
            );


        Assert.Equal(localFolderThatWasDeletedContents, serviceArticles);

        this.service.FoldersClient.FolderDB.DeleteFolder(serviceUserFolder.LocalId);

        // Make some changes in another folder to make sure the changes sync
        var localUserFolderThatIsntTheDeletedOne = this.databases.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId!.Value != serviceUserFolder.ServiceId!.Value);
        var localFirstArticle = this.databases.ArticleDB.FirstArticleInFolder(localUserFolderThatIsntTheDeletedOne.LocalId);
        localFirstArticle = this.databases.ArticleDB.UpdateReadProgressForArticle(localFirstArticle.ReadProgress + 0.5F, DateTime.Now, localFirstArticle.Id);

        await this.syncEngine.SyncBookmarks();

        // Check the deleted folder wasn't deleted locally, and that it's contents match
        var localFolderThatWasDeletedPostSync = this.databases.FolderDB.GetFolderByServiceId(serviceUserFolder.ServiceId!.Value)!;
        Assert.NotNull(localFolderThatWasDeletedPostSync);

        var localFolderThatWasDeletedContentsPostSync = this.databases.ArticleDB.ListArticlesForLocalFolder(localFolderThatWasDeletedPostSync.LocalId);
        Assert.Equal(localFolderThatWasDeletedContents, localFolderThatWasDeletedContentsPostSync);

        // Check the article that we changed the progress of was updated
        var localFirstArticlePostSync = this.databases.ArticleDB.GetArticleById(localFirstArticle.Id)!;
        Assert.Equal(localFirstArticle.ReadProgress, localFirstArticlePostSync.ReadProgress);
        Assert.Equal(localFirstArticle.ReadProgressTimestamp, localFirstArticlePostSync.ReadProgressTimestamp);
    }
    #endregion

    #region List-Limits
    [Fact]
    public async Task ArticlesOutsideThePerFolderLimitAreDeletedLocally()
    {
        var preSyncArticleCount = this.databases.ArticleDB.ListAllArticlesInAFolder().Count;

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncBookmarks();

        var postSyncArticleCount = this.databases.ArticleDB.ListAllArticlesInAFolder().Count;
        Assert.True(postSyncArticleCount < preSyncArticleCount);
    }

    private void UnlikeEverything()
    {
        foreach(var article in this.databases.ArticleDB.ListLikedArticles())
        {
            this.databases.ArticleDB.UnlikeArticle(article.Id);
        }

        foreach(var article in this.service.BookmarksClient.ArticleDB.ListLikedArticles())
        {
            this.service.BookmarksClient.ArticleDB.UnlikeArticle(article.Id);
        }
    }

    [Fact]
    public async Task LikedArticlesOutsidePerFolderLimitAreStillAvailableLocallyAfterSync()
    {
        this.UnlikeEverything();

        var serviceLikedArticle = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Last()!;
        serviceLikedArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(serviceLikedArticle.Id);

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncBookmarks();
        this.syncEngine.CleanupOrphanedArticles();

        // Check the article is no longer in the folder
        var localUnreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.DoesNotContain(serviceLikedArticle, localUnreadArticles);

        // Check that the article is still in the liked list
        var localLikedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(serviceLikedArticle, localLikedArticles);
    }

    [Fact]
    public async Task ArticlesThatAreOrphanedAreDeletedWhenCleanedup()
    {
        this.UnlikeEverything();
        var preSyncArticles = this.databases.ArticleDB.ListAllArticlesInAFolder();

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncBookmarks();
        this.syncEngine.CleanupOrphanedArticles();

        var postSyncArticle = this.databases.ArticleDB.ListAllArticlesInAFolder();
        Assert.True(postSyncArticle.Count < preSyncArticles.Count);

        foreach(var unreachable in preSyncArticles.Except(postSyncArticle))
        {
            Assert.Null(this.databases.ArticleDB.GetArticleById(unreachable.Article.Id));
        }
    }
    #endregion
}