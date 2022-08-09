using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid.Sync;

public class BookmarkSyncTests : BaseSyncTest
{
    #region Local-Only Article Pending Changes
    [Fact]
    public async Task PendingArticleAddIsAddedRemote()
    {
        this.SwitchToEmptyLocalDatabase();
        this.SwitchToEmptyServiceDatabase();

        // Add an article directly to pending adds, since we don't have a way to
        // add a 'complete' article explicitly ('cause the service needs to do
        // much of the work)
        var addedUrl = TestUtilities.GetRandomUrl();
        this.databases.ArticleChangesDB.CreatePendingArticleAdd(addedUrl, null);

        // Sync
        await this.syncEngine.SyncArticles();

        // Make sure we can see it on the service
        var remoteArticles = this.service.BookmarksClient.ArticleDB.ListAllArticlesInAFolder();
        Assert.Single(remoteArticles);
        Assert.Equal(addedUrl, remoteArticles.First()!.Article.Url);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    #region Deletes
    [Fact]
    public async Task PendingArticleDeletesInUnreadFolderAreRemovedRemotely()
    {
        // Delete a known article
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's gone
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(remoteArticle);
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleDeletesInUnreadFolderAreCompletedEvenIfArticleMissingRemotely()
    {
        // Delete a known article
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Delete it from the service mock
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's really gone from the service
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(remoteArticle);

        // Make suer pending edits are cleaned up
        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleDeletesInCustomFolderAreRemovedRemotely()
    {
        // Folder we'll add to
        var firstUserFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Delete a known article from that folder
        var ledger = this.GetLedger();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstUserFolder.LocalId);
        this.databases.ArticleDB.DeleteArticle(firstUnreadArticle.Id);
        ledger.Dispose();

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's gone
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.Null(remoteArticle);
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
        await this.syncEngine.SyncArticles();

        // Check in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstUnreadArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButArticleMissingRemotely()
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
        await this.syncEngine.SyncArticles();

        // Check it's gone
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadFolderToCustomFolderButFolderIsMissingRemotely()
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
        await this.syncEngine.SyncPendingArticleMoves();

        // Check it's gone
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.DoesNotContain(firstUnreadArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnread()
    {
        // Delete a known article
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticleInCustomFolder = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticleInCustomFolder.Id, WellKnownLocalFolderIds.Unread);
        }

        // Sync
        await this.syncEngine.SyncArticles();

        // Check in the unread folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArticleInCustomFolder, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromCustomFolderToUnreadButArticleMissingRemotelyCompletesWithArticleInUnreadFolderLocally()
    {
        // Select an article to move
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move it
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Delete it from the service, so it will fail when we try to apply the
        // move on the service.
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstArticle.Id);

        // Sync
        await this.syncEngine.SyncPendingArticleMoves();

        // Check the article is not in a folder, for later clean up
        var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
        Assert.DoesNotContain(firstArticle, orphanedArticles);

        var unreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(unreadArticles, (a) => a.Url == firstArticle.Url);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadToArchiveHandledAsAnArchive()
    {
        // Delete a known article
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Sync
        await this.syncEngine.SyncArticles();

        // Check in the archive folder folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(firstUnreadArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadToArchiveButArticleMissingCompletes()
    {
        // Move an article to a folder...
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // ... delete that article
        this.service.BookmarksClient.ArticleDB.DeleteArticle(firstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncArticles();

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
        await this.syncEngine.SyncArticles();

        // Check in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(firstArchiveArticle, remoteArticlesInFolder);

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
        await this.syncEngine.SyncArticles();

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
        await this.syncEngine.SyncArticles();

        // Check in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(firstArchivedArticle, remoteArticlesInFolder);

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
        await this.syncEngine.SyncArticles();

        // Check article still available remotely
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id);
        Assert.NotNull(remoteArticle);

        // Get the new folder from remote
        var remoteFolder = this.service.FoldersClient.FolderDB.GetFolderByTitle(unsyncedNewFolder.Title);
        Assert.NotNull(remoteFolder);

        // Check the article is actually in the folder now
        var removeFolderContents = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFolder!.LocalId);
        Assert.Contains(remoteArticle, removeFolderContents);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleMoveFromUnreadSyncingAlsoAppliesRemoteArticlePropertyChanges()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Move article to a custom folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, firstFolder.LocalId);
        }

        // Make additional changes to that article on the service
        var remoteUpdatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(firstFolder.LocalId);
        Assert.Contains(remoteUpdatedArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(remoteUpdatedArticle, firstUnreadArticle);
    }

    [Fact]
    public async Task PendingArticleMoveToUnreadSyncingAlsoAppliesRemoteArticlePropertyChanges()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move article to unread folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Unread);
        }

        // Make additional changes to that article on the service
        var remoteUpdatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Contains(remoteUpdatedArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(remoteUpdatedArticle, firstUnreadArticle);
    }

    [Fact]
    public async Task PendingArticleMoveToArchiveSyncingAlsoAppliesRemoteArticlePropertyChanges()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        var firstUnreadArticle = this.databases.ArticleDB.FirstArticleInFolder(firstFolder.LocalId);

        // Move article to unread folder
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.MoveArticleToFolder(firstUnreadArticle.Id, WellKnownLocalFolderIds.Archive);
        }

        // Make additional changes to that article on the service
        var remoteUpdatedArticle = this.service.BookmarksClient.ArticleDB.UpdateArticle(DatabaseArticle.ToArticleRecordInformation(firstUnreadArticle with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now,
            Liked = true,
            Hash = "NEWHASH",
        }));

        // Sync
        await this.syncEngine.SyncArticles();

        // Check it's in the new folder
        var remoteArticlesInFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Contains(remoteUpdatedArticle, remoteArticlesInFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();

        // Get the article locally again
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id);

        // Check that all the status changes have now being synced
        Assert.Equal(remoteUpdatedArticle, firstUnreadArticle);
    }
    #endregion

    #region Liking
    [Fact]
    public async Task PendingArticleLikeSyncsToRemote()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is liked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(remoteArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeButAlreadyLikedRemotely()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        _ = this.service.BookmarksClient.ArticleDB.LikeArticle(firstUnreadArticle.Id);

        // Like the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is liked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(remoteArticle.Liked);

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
        await this.syncEngine.SyncArticles();

        // Check the article is liked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(remoteArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeSyncsRemotely()
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
        await this.syncEngine.SyncArticles();

        // Check the article is unliked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(remoteArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeButAlreadyUnlikedRemotely()
    {
        var firstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        firstUnreadArticle = this.databases.ArticleDB.LikeArticle(firstUnreadArticle.Id); // Make sure its liked so we generate a pending edit


        // Unlike the article
        using (var ledger = this.GetLedger())
        {
            this.databases.ArticleDB.UnlikeArticle(firstUnreadArticle.Id);
        }

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is unliked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.False(remoteArticle.Liked);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleUnlikeMissingArticleDoesntAddArticleRemotely()
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
        await this.syncEngine.SyncArticles();

        // Check the article is not re-added
        var remoteArticle = this.service.BookmarksClient.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == firstUnreadArticle.Url);
        Assert.Null(remoteArticle);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingArticleLikeSyncPicksUpOtherPropertyChangesRemotely()
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
        await this.syncEngine.SyncArticleLikeStatuses();

        // Check the article is liked
        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.True(remoteArticle.Liked);

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

    #region Remote-Only Article Changes
    [Fact]
    public async Task RemoteArticleAddedToUnreadIsAddedLocally()
    {
        var addedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb();

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(addedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(addedArticle, localArticle);

        // Check that service & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        var remoteUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(remoteUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleDeletedFromUnreadIsRemovedLocally()
    {
        var remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteFirstUnreadArticle.Id);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteFirstUnreadArticle.Id);
        if (localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }

        // Check that remote & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        var remoteUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(remoteUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleAddedToArchiveIsAddedLocally()
    {
        var remoteAddedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb(WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteAddedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteAddedArticle, localArticle);

        // Check that remote & local agree on unread contents
        var localArchive = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        var remoteArchive = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(remoteArchive, localArchive);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleDeletedFromArchiveIsRemovedLocally()
    {
        var remoteFirstArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteFirstArchiveArticle.Id);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteFirstArchiveArticle.Id);
        if (localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }

        // Check that remote & local agree on unread contents
        var localArchive = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        var remoteArchive = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(remoteArchive, localArchive);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleAddedToUserFolderIsAddedLocally()
    {
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;
        var remoteAddedArticle = this.service.BookmarksClient.ArticleDB.AddRandomArticleToDb(remoteFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available now
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteAddedArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteAddedArticle, localArticle);

        // Check that remote & local agree on unread contents
        var localUnread = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        var remoteUnread = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(remoteUnread, localUnread);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleDeletedFromUserFolderIsRemovedLocally()
    {
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;
        var remoteArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(remoteFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteArticle.Id);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is missing
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteArticle.Id);
        if (localArticle is not null)
        {
            // If the article has been deleted, thats OK. But if it hasn't, we
            // need to check it hasn't been placed in a folder
            var orphanedArticles = this.databases.ArticleDB.ListArticlesNotInAFolder();
            Assert.Contains(localArticle, orphanedArticles);
        }

        // Check that remote & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromUnreadToUserFolderIsMovedLocally()
    {
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;
        var remoteUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteUnreadArticle.Id, remoteFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteUnreadArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteUnreadArticle, localArticle);

        // Check that remote & local agree on custom folder contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromUnreadToArchiveIsMovedLocally()
    {
        var remoteUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteUnreadArticle.Id, WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteUnreadArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteUnreadArticle, localArticle);

        // Check that remote & local agree on Archive contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromUserFolderToUnreadIsMovedLocally()
    {
        // Select a folder to move from
        var serviceFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFirstUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to unread
        var remoteUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(serviceFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteUserArticle.Id, WellKnownLocalFolderIds.Unread);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteUserArticle, localArticle);

        // Check that remote & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromUserFolderToArchiveIsMovedLocally()
    {
        // Select a folder to move from
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to archive
        var remoteUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(remoteFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteUserArticle.Id, WellKnownLocalFolderIds.Archive);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteUserArticle, localArticle);

        // Check that remote & local agree on unread contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromUserFolderToUserFolderIsMovedLocally()
    {
        // Select a folder to move from
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;

        // Select a folder to move to
        var remoteSecondUserFolder = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId != remoteFirstUserFolder.ServiceId);
        var localSecondUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteSecondUserFolder.ServiceId!.Value)!;

        // Move the first article in that folder to the second folder
        var remoteUserArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(remoteFirstUserFolder.LocalId);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteUserArticle.Id, remoteSecondUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteUserArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteUserArticle, localArticle);

        // Check that remote & local agree on second folder contents contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localSecondUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteSecondUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromArchiveToUserFolderIsMovedLocally()
    {
        var remoteFirstUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var localFirstUserFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFirstUserFolder.ServiceId!.Value)!;
        var remoteArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteArchiveArticle.Id, remoteFirstUserFolder.LocalId);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteArchiveArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteArchiveArticle, localArticle);

        // Check that remote & local agree on custom folder contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(localFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFirstUserFolder.LocalId).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task RemoteArticleMovedFromArchiveToUnreadIsMovedLocally()
    {
        var remoteArchiveArticle = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteArchiveArticle.Id, WellKnownLocalFolderIds.Unread);

        // Sync
        await this.syncEngine.SyncArticles();

        // Check the article is available
        var localArticle = this.databases.ArticleDB.GetArticleById(remoteArchiveArticle.Id);
        Assert.NotNull(localArticle);
        Assert.Equal(remoteArchiveArticle, localArticle);

        // Check that remote & local agree on Archive contents
        var localFolder = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Contains(localArticle, localFolder);

        var remoteFolder = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).OrderBy((a) => a.Id);
        Assert.Equal(remoteFolder, localFolder);

        this.databases.ArticleChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #region List API-based property updates
    [Fact]
    public async Task LocalProgressChangeInUnreadNewerThanRemoteIsAppliedRemotely()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.Equal(newProgress, firstUnreadArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstUnreadArticle.ReadProgressTimestamp);
        Assert.Equal(firstUnreadArticle, remoteArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUnreadOlderThanRemoteIsAppliedLocally()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        firstUnreadArticle = this.databases.ArticleDB.GetArticleById(firstUnreadArticle.Id)!;
        Assert.NotEqual(newProgress, firstUnreadArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstUnreadArticle.ReadProgressTimestamp);
        Assert.Equal(firstUnreadArticle, remoteArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInArchiveNewerThanRemoteIsAppliedToRemotely()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.Equal(newProgress, firstArchiveArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, remoteArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInArchiveOlderThanRemoteIsAppliedLocally()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.NotEqual(newProgress, firstArchiveArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, remoteArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUserFolderNewerThanRemoteIsAppliedRemotely()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.Equal(newProgress, firstArchiveArticle.ReadProgress);
        Assert.Equal(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, remoteArticle);
    }

    [Fact]
    public async Task LocalProgressChangeInUserFolderOlderThanRemoteIsAppliedLocally()
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

        await this.syncEngine.SyncArticles();

        var remoteArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        firstArchiveArticle = this.databases.ArticleDB.GetArticleById(firstArchiveArticle.Id)!;
        Assert.NotEqual(newProgress, firstArchiveArticle.ReadProgress);
        Assert.NotEqual(newProgressTimestamp, firstArchiveArticle.ReadProgressTimestamp);
        Assert.Equal(firstArchiveArticle, remoteArticle);
    }

    [Fact]
    public async Task LikedRemoteArticleIsLikedLocallyAfterSync()
    {
        var remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteFirstUnreadArticle.Id);

        await this.syncEngine.SyncArticles();

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteFirstUnreadArticle.Id)!;
        Assert.True(localArticle.Liked);
        Assert.Equal(remoteFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(localArticle, likedArticles);
    }

    [Fact]
    public async Task LikedRemoteArticleThatIsNotInAFolderIsCorrectlySynced()
    {
        // Find a remote article, and like it
        var remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteFirstUnreadArticle.Id);

        // Create a *liked* article that is no longer in any folder. The
        // documentation says articles in folders should be moved to the archive
        // folder when the containing folder is deleted. But, they aren't placed
        // anywhere (the bug). This introduces a scenario where liked articles
        // now *only* appear in the liked 'virtual' folder. Since this has been
        // happening for ~3 years, we should mimick the bug in our local testing
        this.service.BookmarksClient.ArticleDB.RemoveArticleFromAnyFolder(remoteFirstUnreadArticle.Id);

        await this.syncEngine.SyncArticles();

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteFirstUnreadArticle.Id)!;
        Assert.True(localArticle.Liked);
        Assert.Equal(remoteFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(localArticle, likedArticles);

        // Check that article isn't in the unread Folder
        var unreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.DoesNotContain(localArticle, unreadArticles);
    }

    [Fact]
    public async Task LikedRemoteArticleThatIsNotInAFolderIsCorrectlySyncedWithEmptyLocalDatabase()
    {
        this.SwitchToEmptyLocalDatabase();

        // Find a remote article, and like it
        var remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        remoteFirstUnreadArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteFirstUnreadArticle.Id);

        // Create a *liked* article that is no longer in any folder. The
        // documentation says articles in folders should be moved to the archive
        // folder when the containing folder is deleted. But, they aren't placed
        // anywhere (the bug). This introduces a scenario where liked articles
        // now *only* appear in the liked 'virtual' folder. Since this has been
        // happening for ~3 years, we should mimick the bug in our local testing
        this.service.BookmarksClient.ArticleDB.RemoveArticleFromAnyFolder(remoteFirstUnreadArticle.Id);

        await this.syncEngine.SyncArticles();

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteFirstUnreadArticle.Id)!;
        Assert.True(localArticle.Liked);
        Assert.Equal(remoteFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(localArticle, likedArticles);

        // Check that article isn't in the unread Folder
        var unreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.DoesNotContain(localArticle, unreadArticles);
    }

    [Fact]
    public async Task UnlikedRemoteArticleIsThatIsLikedLocallyBecomesUnlikedAfterSync()
    {
        var localFirstUnreadArticle = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleDB.LikeArticle(localFirstUnreadArticle.Id);

        await this.syncEngine.SyncArticles();

        var localArticle = this.databases.ArticleDB.GetArticleById(localFirstUnreadArticle.Id)!;
        Assert.False(localArticle.Liked);
        Assert.Equal(localFirstUnreadArticle, localArticle);

        var likedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.DoesNotContain(localArticle, likedArticles);
    }

    [Fact]
    public async Task SyncingWithARemoteDeletedFolderCompletes()
    {
        // Makes sure liked articles don't confuse this test. This test expects
        // the article state to remain the same, but because *like* syncing will
        // see the article is no longer liked -- but the general sync due to the
        // missing folder won't delete it -- and unlike it. and thus we have
        // in consistent state. In a more realistic scenario, the deleted folder
        // would have been cleaned up, and the articles would be deleted. But,
        // we're testing a pathological case, so we'll make this exception.
        this.UnlikeEverything();

        // Get the remote folder to delete, and all it's articles.
        var remoteUserFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var remoteArticles = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteUserFolder.LocalId);

        foreach (var remoteArticle in this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteUserFolder.LocalId))
        {
            this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteArticle.Id);
        }

        var localFolderThatWasDeletedContents =
            this.databases.ArticleDB.ListArticlesForLocalFolder(
                this.databases.FolderDB.GetFolderByServiceId(remoteUserFolder.ServiceId!.Value)!.LocalId
            );


        Assert.Equal(localFolderThatWasDeletedContents, remoteArticles);

        this.service.FoldersClient.FolderDB.DeleteFolder(remoteUserFolder.LocalId);

        // Make some changes in another folder to make sure the changes sync
        var localUserFolderThatIsntTheDeletedOne = this.databases.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId!.Value != remoteUserFolder.ServiceId!.Value);
        var localFirstArticle = this.databases.ArticleDB.FirstArticleInFolder(localUserFolderThatIsntTheDeletedOne.LocalId);
        localFirstArticle = this.databases.ArticleDB.UpdateReadProgressForArticle(localFirstArticle.ReadProgress + 0.5F, DateTime.Now, localFirstArticle.Id);

        await this.syncEngine.SyncArticles();

        // Check the deleted folder wasn't deleted locally, and that it's contents match
        var localFolderThatWasDeletedPostSync = this.databases.FolderDB.GetFolderByServiceId(remoteUserFolder.ServiceId!.Value)!;
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
        var preSyncArticleCount = this.databases.ArticleDB.ListAllArticlesInAFolder().Count();

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncArticles();

        var postSyncArticleCount = this.databases.ArticleDB.ListAllArticlesInAFolder().Count();
        Assert.True(postSyncArticleCount < preSyncArticleCount);
    }

    private void UnlikeEverything()
    {
        foreach (var article in this.databases.ArticleDB.ListLikedArticles())
        {
            this.databases.ArticleDB.UnlikeArticle(article.Id);
        }

        foreach (var article in this.service.BookmarksClient.ArticleDB.ListLikedArticles())
        {
            this.service.BookmarksClient.ArticleDB.UnlikeArticle(article.Id);
        }
    }

    [Fact]
    public async Task LikedArticlesOutsidePerFolderLimitAreStillAvailableLocallyAfterSync()
    {
        this.UnlikeEverything();

        var remoteLikedArticle = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Last()!;
        remoteLikedArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteLikedArticle.Id);

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncArticles();
        this.syncEngine.CleanupOrphanedArticles();

        // Check the article is no longer in the folder
        var localUnreadArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.DoesNotContain(remoteLikedArticle, localUnreadArticles);

        // Check that the article is still in the liked list
        var localLikedArticles = this.databases.ArticleDB.ListLikedArticles();
        Assert.Contains(remoteLikedArticle, localLikedArticles);
    }

    [Fact]
    public async Task ArticlesThatAreOrphanedAreDeletedWhenCleanedup()
    {
        this.UnlikeEverything();
        var preSyncArticles = this.databases.ArticleDB.ListAllArticlesInAFolder();

        this.syncEngine.ArticlesPerFolderToSync = 1;

        await this.syncEngine.SyncArticles();
        this.syncEngine.CleanupOrphanedArticles();

        var postSyncArticle = this.databases.ArticleDB.ListAllArticlesInAFolder();
        Assert.True(postSyncArticle.Count() < preSyncArticles.Count());

        foreach (var unreachable in preSyncArticles.Except(postSyncArticle))
        {
            Assert.Null(this.databases.ArticleDB.GetArticleById(unreachable.Article.Id));
        }
    }
    #endregion

    #region Cancellation
    [Fact]
    public async Task CancellingArticleSyncPriorToFirstAddDoesNotSyncAnything()
    {
        this.SwitchToEmptyLocalDatabase();
        this.SwitchToEmptyServiceDatabase();

        this.databases.ArticleChangesDB.CreatePendingArticleAdd(TestUtilities.GetRandomUrl(), null);

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingAdd += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleAdds());
        Assert.Empty(this.service.BookmarksClient.ArticleDB.ListAllArticles());
    }

    [Fact]
    public async Task CancellingArticleSyncAfterFirstAddDoesNotSyncAnythingElse()
    {
        this.SwitchToEmptyLocalDatabase();
        this.SwitchToEmptyServiceDatabase();

        var addedUrl = TestUtilities.GetRandomUrl();
        this.databases.ArticleChangesDB.CreatePendingArticleAdd(addedUrl, null);
        this.databases.ArticleChangesDB.CreatePendingArticleAdd(TestUtilities.GetRandomUrl(), null);

        CancellationTokenSource source = new CancellationTokenSource();
        var eventCount = 0;
        this.syncEngine.__Hook_ArticleSync_PrePendingAdd += (_, _) =>
        {
            eventCount += 1;
            if (eventCount == 2)
            {
                source.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleAdds());
        Assert.Contains(this.service.BookmarksClient.ArticleDB.ListAllArticles(), (a) => a.Url == addedUrl);
    }

    [Fact]
    public async Task CancellingArticleDeleteBeforeFirstDeleteDoesNotSyncAnything()
    {
        var articleToDelete = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        var ledger = this.GetLedger();
        this.databases.ArticleDB.DeleteArticle(articleToDelete.Id);
        ledger.Dispose();

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingDelete += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        Assert.NotNull(this.service.BookmarksClient.ArticleDB.GetArticleById(articleToDelete.Id));
        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleDeletes());
    }

    [Fact]
    public async Task CancellingArticleDeleteBeforeSecondDeleteDoesNotSyncAnythingMove()
    {
        var articleToDelete = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        var ledger = this.GetLedger();
        this.databases.ArticleDB.DeleteArticle(articleToDelete.Id);

        var articleToDelete2 = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        this.databases.ArticleDB.DeleteArticle(articleToDelete2.Id);
        ledger.Dispose();

        CancellationTokenSource source = new CancellationTokenSource();
        var eventCount = 0;

        this.syncEngine.__Hook_ArticleSync_PrePendingDelete += (_, _) =>
        {
            eventCount += 1;

            if (eventCount == 2)
            {
                source.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        Assert.Null(this.service.BookmarksClient.ArticleDB.GetArticleById(articleToDelete.Id));
        Assert.NotNull(this.service.BookmarksClient.ArticleDB.GetArticleById(articleToDelete2.Id));
        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleDeletes());
    }

    [Fact]
    public async Task CancellingArticleMoveBeforeFirstMoveDoesNotSyncAnything()
    {
        var articleToMove = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var destinationFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        
        var ledger = this.GetLedger();
        this.databases.ArticleDB.MoveArticleToFolder(articleToMove.Id, destinationFolder.LocalId);
        ledger.Dispose();

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSyncPrePendingMove += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var remoteDestinationFolder = this.service.FoldersClient.FolderDB.GetFolderByServiceId(destinationFolder.ServiceId!.Value)!;
        Assert.DoesNotContain(
            this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteDestinationFolder.LocalId),
            (a) => a.Id == articleToMove.Id
        );

        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleMovesForLocalFolderId(destinationFolder.LocalId));
    }

    [Fact]
    public async Task CancellingArticleMoveBeforeSecondMoveDoesNotSyncAnythingMore()
    {
        var articleToMove = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var destinationFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        
        var ledger = this.GetLedger();
        this.databases.ArticleDB.MoveArticleToFolder(articleToMove.Id, destinationFolder.LocalId);
        ledger.Dispose();

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSyncPrePendingMove += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var remoteDestinationFolder = this.service.FoldersClient.FolderDB.GetFolderByServiceId(destinationFolder.ServiceId!.Value)!;
        Assert.DoesNotContain(
            this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteDestinationFolder.LocalId),
            (a) => a.Id == articleToMove.Id
        );

        Assert.Single(this.databases.ArticleChangesDB.ListPendingArticleMovesForLocalFolderId(destinationFolder.LocalId));
    }

    [Fact]
    public async Task CancellingArticleForFolderSyncBeforeFirstFolderDoesntSyncAnything()
    {
        var remoteArticleToMutate = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        remoteArticleToMutate = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(0.9F, DateTime.Now, remoteArticleToMutate.Id);

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSyncPreFolder += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteArticleToMutate.Id)!;
        Assert.NotEqual(remoteArticleToMutate, localArticle);
    }

    [Fact]
    public async Task CancellingArticleSyncForFolderBeforeSecondFolderDoesntSyncAnythingMore()
    {
        var remoteArticleToMutate = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        remoteArticleToMutate = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(0.9F, DateTime.Now, remoteArticleToMutate.Id);

        var remoteArticleToMutate2 = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(this.service.FoldersClient.FolderDB.FirstCompleteUserFolder().LocalId);
        remoteArticleToMutate2 = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(0.1F, DateTime.Now, remoteArticleToMutate2.Id);

        CancellationTokenSource source = new CancellationTokenSource();

        var eventCount = 0;
        this.syncEngine.__Hook_ArticleSyncPreFolder += (_, _) =>
        {
            eventCount += 1;
            if (eventCount == 2)
            {
                source.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteArticleToMutate.Id)!;
        Assert.Equal(remoteArticleToMutate, localArticle);

        var localArticle2 = this.databases.ArticleDB.GetArticleById(remoteArticleToMutate2.Id);
        Assert.NotEqual(remoteArticleToMutate2, localArticle2);
    }

    [Fact]
    public async Task CancellingLikeSyncingBeforeFirstSyncDoesntSyncAnything()
    {
        var localArticleToLike = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        using(var ledger = this.GetLedger())
        {
            localArticleToLike = this.databases.ArticleDB.LikeArticle(localArticleToLike.Id);
        }

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSyncPreLike += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var remoteLikedArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(localArticleToLike.Id)!;
        Assert.False(remoteLikedArticle.Liked);
    }

    [Fact]
    public async Task CancellingLikeSyncingBeforeSecondSyncDoesntSyncAnythingMore()
    {
        var localArticleToLike = this.databases.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        var localArticleToLike2 = this.databases.ArticleDB.FirstUnlikedArticleInfolder(this.databases.FolderDB.FirstCompleteUserFolder().LocalId);

        using(var ledger = this.GetLedger())
        {
            localArticleToLike = this.databases.ArticleDB.LikeArticle(localArticleToLike.Id);
            localArticleToLike2 = this.databases.ArticleDB.LikeArticle(localArticleToLike2.Id);
        }

        CancellationTokenSource source = new CancellationTokenSource();
        var eventCount = 0;

        this.syncEngine.__Hook_ArticleSyncPreLike += (_, _) =>
        {
            eventCount += 1;

            if (eventCount == 2)
            {
                source.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        // The order in which the pending changes are synced is 'undefined'. This
        // means the order is potentially reversed. At the time of authoring,
        // the first article to be sync'd is actually the second change. 🤷
        var remoteLikedArticle = this.service.BookmarksClient.ArticleDB.GetArticleById(localArticleToLike.Id)!;
        Assert.False(remoteLikedArticle.Liked);

        var remoteLikedArticle2 = this.service.BookmarksClient.ArticleDB.GetArticleById(localArticleToLike2.Id)!;
        Assert.True(remoteLikedArticle2.Liked);
    }

    [Fact]
    public async Task CancellingLikeSyncingBeforeCollectingRemoteChangesDoesntSyncRemoteChanges()
    {
        var remoteLikedArticle = this.service.BookmarksClient.ArticleDB.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        remoteLikedArticle = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteLikedArticle.Id);

        CancellationTokenSource source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSyncPreRemoteLikeFolderSync += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncArticles(source.Token));

        var localArticle = this.databases.ArticleDB.GetArticleById(remoteLikedArticle.Id)!;
        Assert.False(localArticle.Liked);
    }
    #endregion
}