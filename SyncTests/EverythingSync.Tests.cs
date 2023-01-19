using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid.Sync;

public class EverythingSyncTests : BaseSyncTest
{
    private void AssertServerAndClientMatch()
    {
        // Check folders match
        var remoteFolders = this.service.FoldersClient.FolderDB.ListAllFolders();
        var localFolders = this.databases.FolderDB.ListAllFolders();
        Assert.Equal(remoteFolders, localFolders, new CompareFoldersIgnoringLocalId());

        // Check Articles match
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListAllArticles(), this.databases.ArticleDB.ListAllArticles());
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListLikedArticles(), this.databases.ArticleDB.ListLikedArticles());

        // Check those articles are in the right folders
        foreach (var remoteFolder in remoteFolders)
        {
            var localFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFolder.ServiceId!.Value)!;
            var remoteArticles = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFolder.LocalId);
            var localArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(localFolder.LocalId);
            Assert.Equal(remoteArticles, localArticles);
        }
    }

    [Fact]
    public async Task EmptySyncForDefaultDatabasePullsDownEverythingAndStateMatchesAfter()
    {
        this.SwitchToEmptyLocalDatabase();

        await this.syncEngine.SyncEverythingAsync();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task AllClearingHouseEventsAreRaisedDuringSync()
    {
        this.SwitchToEmptyLocalDatabase();

        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);

        (
            bool SyncStarted,
            bool FoldersStarted,
            bool FoldersEnded,
            bool ArticlesStarted,
            bool ArticlesEnded,
            bool SyncEnded
        ) raisedEvents = (false, false, false, false, false, false);

        clearingHouse.SyncStarted += (_, _) => raisedEvents.SyncStarted = true;
        clearingHouse.FoldersStarted += (_, _) => raisedEvents.FoldersStarted = true;
        clearingHouse.FoldersEnded += (_, _) => raisedEvents.FoldersEnded = true;
        clearingHouse.ArticlesStarted += (_, _) => raisedEvents.ArticlesStarted = true;
        clearingHouse.ArticlesEnded += (_, _) => raisedEvents.ArticlesEnded = true;
        clearingHouse.SyncEnded += (_, _) => raisedEvents.SyncEnded = true;

        await this.syncEngine.SyncEverythingAsync();

        Assert.True(raisedEvents.SyncStarted);
        Assert.True(raisedEvents.FoldersStarted);
        Assert.True(raisedEvents.FoldersEnded);
        Assert.True(raisedEvents.ArticlesStarted);
        Assert.True(raisedEvents.ArticlesEnded);
        Assert.True(raisedEvents.SyncEnded);
    }

    [Fact]
    public async Task EmptyServiceEmptiesTheLocalDatabase()
    {
        this.SwitchToEmptyServiceDatabase();

        await this.syncEngine.SyncEverythingAsync();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task LocalAndRemoteChangesReconcileDuringAFullSync()
    {
        // Local folder to delete
        var localFolderToDelete = this.databases.FolderDB.FirstCompleteUserFolder();

        // Local article to update
        var localArticleToUpdate = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        // Remote article that will be updated
        var remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id != localArticleToUpdate.Id)!;

        // Remote Folder (and it's local verison) to move an article to
        var remoteFolderToMoveTo = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId != localFolderToDelete.ServiceId)!;
        var localFolderToMoveTo = this.databases.FolderDB.GetFolderByServiceId(remoteFolderToMoveTo.ServiceId!.Value)!;

        // Article to move into a remote folder
        var remoteArticleToMove = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);

        // Remote folder to be deleted
        var remoteFolderToDelete = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) =>
        {
            return (f.ServiceId != localFolderToDelete.ServiceId) && (f.ServiceId != remoteFolderToMoveTo.ServiceId);
        })!;

        // Perform remote changes
        remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(remoteArticleToUpdate.ReadProgress + 0.4F, DateTime.Now, remoteArticleToUpdate.Id);
        remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteArticleToUpdate.Id);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteArticleToMove.Id, remoteFolderToMoveTo.LocalId);

        foreach (var remoteArticleToDelete in this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFolderToDelete.LocalId))
        {
            this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteArticleToDelete.Id);
        }

        this.service.FoldersClient.FolderDB.DeleteFolder(remoteFolderToDelete.LocalId);

        // Perform local changes
        using (this.GetLedger())
        {
            this.databases.FolderDB.DeleteFolder(localFolderToDelete.LocalId);
            localArticleToUpdate = this.databases.ArticleDB.UpdateReadProgressForArticle(localArticleToUpdate.ReadProgress + 0.8F, DateTime.Now, localArticleToUpdate.Id);
            localArticleToUpdate = this.databases.ArticleDB.LikeArticle(localArticleToUpdate.Id);
        }

        await this.syncEngine.SyncEverythingAsync();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task MegaTransactionCanRollBackLocalDatabaseIfFailureObserved()
    {
        this.SwitchToEmptyServiceDatabase();

        var transaction = this.StartTransactionForLocalDatabase();

        await this.syncEngine.SyncEverythingAsync();

        this.AssertServerAndClientMatch();

        transaction.Rollback();
        transaction.Dispose();

        Assert.NotEmpty(this.databases.FolderDB.ListAllCompleteUserFolders());
        Assert.NotEmpty(this.databases.ArticleDB.ListAllArticles());
    }

    [Fact]
    public async Task InitiatingSyncSecondTimeBeforeFirstCompletesReturnsSameTask()
    {
        this.SwitchToEmptyLocalDatabase();
        this.service.FoldersClient.DelayAsyncOperations = true;

        var firstSync = this.syncEngine.SyncEverythingAsync();
        var secondSync = this.syncEngine.SyncEverythingAsync();

        Assert.Same(firstSync, secondSync);

        await firstSync;
        await secondSync;

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task InitiatingSyncTwiceAfterFirstCompletesReturnsDifferentTask()
    {
        this.SwitchToEmptyLocalDatabase();
        this.service.FoldersClient.DelayAsyncOperations = true;

        var firstSync = this.syncEngine.SyncEverythingAsync();
        await firstSync;

        var secondSync = this.syncEngine.SyncEverythingAsync();

        Assert.NotSame(firstSync, secondSync);

        await secondSync;

        this.AssertServerAndClientMatch();
    }

    #region Cancellation
    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingAddCompletesWithOperationCancelledException()
    {
        this.databases.ArticleChangesDB.CreatePendingArticleAdd(TestUtilities.GetRandomUrl(), null);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingAdd += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingDeleteCompletesWithOperationCancelledException()
    {
        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleDelete(article.Id);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingDelete += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingMoveCompletesWithOperationCancelledException()
    {
        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleMove(article.Id, WellKnownLocalFolderIds.Archive);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingMove += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingLikeCompletesWithOperationCancelledException()
    {
        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleStateChange(article.Id, true);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreLike += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPreRemoteLikeFolderSyncCompletesWithOperationCancelledException()
    {
        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreRemoteLikeFolderSync += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPreFolderCompletesWithOperationCancelledException()
    {
        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleDelete(article.Id);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreFolder += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtFolderPreAddCompletesWithOperationCancelledException()
    {
        var newFolder = this.databases.FolderDB.CreateFolder("Wibble");
        this.databases.FolderChangesDB.CreatePendingFolderAdd(newFolder.LocalId);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_FolderSync_PreSingleAdd += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtFolderPreDeleteCompletesWithOperationCancelledException()
    {
        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderChangesDB.CreatePendingFolderDelete(firstFolder.ServiceId!.Value, firstFolder.Title);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_FolderSync_PreSingleDelete += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingAddRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        this.databases.ArticleChangesDB.CreatePendingArticleAdd(TestUtilities.GetRandomUrl(), null);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingAdd += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingDeleteRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleDelete(article.Id);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingDelete += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingMoveRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleMove(article.Id, WellKnownLocalFolderIds.Archive);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PrePendingMove += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPrePendingLikeRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleStateChange(article.Id, true);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreLike += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPreRemoteLikeFolderSyncRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreRemoteLikeFolderSync += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtArticlesPreFolderRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var article = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.databases.ArticleChangesDB.CreatePendingArticleDelete(article.Id);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_ArticleSync_PreFolder += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtFolderPreAddRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var newFolder = this.databases.FolderDB.CreateFolder("Wibble");
        this.databases.FolderChangesDB.CreatePendingFolderAdd(newFolder.LocalId);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_FolderSync_PreSingleAdd += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }

    [Fact]
    public async Task CancellingSyncAtFolderPreDeleteRaisesSyncError()
    {
        var clearingHouse = new SyncEventClearingHouse();
        this.SetSyncEngineFromDatabases(clearingHouse);
        var errorRaised = false;
        clearingHouse.SyncError += (_, _) => errorRaised = true;

        var firstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderChangesDB.CreatePendingFolderDelete(firstFolder.ServiceId!.Value, firstFolder.Title);

        var source = new CancellationTokenSource();
        this.syncEngine.__Hook_FolderSync_PreSingleDelete += (_, _) => source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => this.syncEngine.SyncEverythingAsync(source.Token));

        Assert.True(errorRaised);
    }
    #endregion
}