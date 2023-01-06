using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.Sync;

public class SyncHelperTests
{
    private class MockInstapaperSync : IInstapaperSync
    {
        private TaskCompletionSource tcs = new TaskCompletionSource();
        public async Task SyncEverythingAsync(CancellationToken cancellationToken = default)
        {
            await this.tcs.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void TriggerSyncEverythingComplete()
        {
            this.tcs.SetResult();
        }
    }

    private class MockArticleDownloader : IArticleDownloader
    {
        private TaskCompletionSource downloadAllWithoutState = new TaskCompletionSource();
        private TaskCompletionSource downloadAllArticles = new TaskCompletionSource();
        private TaskCompletionSource<DatabaseLocalOnlyArticleState?> downloadSingleArticleState = new TaskCompletionSource<DatabaseLocalOnlyArticleState?>();

        public async Task DownloadAllArticlesWithoutLocalStateAsync(CancellationToken cancellationToken = default)
        {
            await this.downloadAllWithoutState.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task<DatabaseLocalOnlyArticleState?> DownloadArticleAsync(DatabaseArticle article, CancellationToken cancellationToken = default)
        {
            var result = await this.downloadSingleArticleState.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        public async Task DownloadArticlesAsync(IEnumerable<DatabaseArticle> articles, CancellationToken cancellationToken = default)
        {
            await this.downloadAllArticles.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void TriggerAll()
        {
            this.downloadAllArticles.SetResult();
            this.downloadAllWithoutState.SetResult();
            this.downloadSingleArticleState.SetResult(null);
        }
    }

    private (SyncHelper, MockArticleDownloader, MockInstapaperSync) GetHelper()
    {
        var sync = new MockInstapaperSync();
        var downloader = new MockArticleDownloader();
        var helper = new SyncHelper(downloader, () =>
        {
            return Task.FromResult<(IInstapaperSync, IDbConnection)>((sync, this.GetDbConnection()));
        });

        return (helper, downloader, sync);
    }

    private IDbConnection GetDbConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        return connection;
    }

    [Fact]
    public void CanInstantiateSyncHelper()
    {
        var (_, _, helper) = this.GetHelper();

        Assert.NotNull(helper);
    }

    [Fact]
    public async Task SyncingCompletesWhenSuccessful()
    {
        var (helper, downloader, sync) = this.GetHelper();
        sync.TriggerSyncEverythingComplete();
        downloader.TriggerAll();
        await helper.SyncDatabaseAndArticles();
    }

    [Fact]
    public async Task WhenCancelledTaskResultIsCancelled()
    {
        var (helper, downloader, sync) = this.GetHelper();

        var syncTask = helper.SyncDatabaseAndArticles();

        helper.Cancel();

        downloader.TriggerAll();
        sync.TriggerSyncEverythingComplete();

        await Assert.ThrowsAsync<OperationCanceledException>(() => syncTask);

        Assert.True(syncTask.IsCanceled);
    }

    [Fact]
    public void IsSyncingIsFalseBeforeSync()
    {
        var (helper, downloader, sync) = this.GetHelper();

        Assert.False(helper.IsSyncing);
    }

    [Fact]
    public async Task IsSyncingIsTrueDuringSync()
    {
        var (helper, downloader, sync) = this.GetHelper();
        var syncTask = helper.SyncDatabaseAndArticles();

        Assert.True(helper.IsSyncing);

        downloader.TriggerAll();
        sync.TriggerSyncEverythingComplete();

        await syncTask;
    }

    [Fact]
    public async Task IsSyncingIsFalseAfterSync()
    {
        var (helper, downloader, sync) = this.GetHelper();
        var syncTask = helper.SyncDatabaseAndArticles();

        Assert.True(helper.IsSyncing);

        downloader.TriggerAll();
        sync.TriggerSyncEverythingComplete();

        await syncTask;

        Assert.False(helper.IsSyncing);
    }

    [Fact]
    public async Task IsSyncingPropertyChangesAreRaisedDuringSync()
    {
        var (helper, downloader, sync) = this.GetHelper();

        List<bool> isSyncingValueChanges = new List<bool>();

        helper.PropertyChanged += (o, a) =>
        {
            if (a.PropertyName != nameof(helper.IsSyncing))
            {
                return;
            }

            isSyncingValueChanges.Add(helper.IsSyncing);
        };


        var syncTask = helper.SyncDatabaseAndArticles();
        downloader.TriggerAll();
        sync.TriggerSyncEverythingComplete();

        await syncTask;

        Assert.Equal(2, isSyncingValueChanges.Count);
        Assert.True(isSyncingValueChanges[0]);
        Assert.False(isSyncingValueChanges[1]);
    }

    [Fact]
    public async Task IsSyncingReturnsFalseAfterCancellation()
    {
        var (helper, downloader, sync) = this.GetHelper();

        var syncTask = helper.SyncDatabaseAndArticles();
        helper.Cancel();

        downloader.TriggerAll();
        sync.TriggerSyncEverythingComplete();

        await Assert.ThrowsAsync<OperationCanceledException>(() => syncTask);

        Assert.False(helper.IsSyncing);
    }

    [Fact]
    public async Task SyncCompletedWhenDownloadingArticlesDisabled()
    {
        var (helper, downloader, sync) = this.GetHelper();
        helper.DownloadArticles = false;

        sync.TriggerSyncEverythingComplete();

        await helper.SyncDatabaseAndArticles();
    }

    [Fact]
    public async Task SyncDoesntCompleteIfDownloadsArentCompletedWhenDownloadingArticlesEnabled()
    {
        var (helper, downloader, sync) = this.GetHelper();
        helper.DownloadArticles = true;

        // Start the task, and trigger moving to the download tasks
        var syncTask = helper.SyncDatabaseAndArticles();
        sync.TriggerSyncEverythingComplete();

        // Download task will block on waiting for the task, if we cancel it now
        // the mocks will throw when the tasks are completed, causing the cancel
        // to propogate correctly.
        //
        // We're only cancelling to get the task to complete!
        helper.Cancel();
        downloader.TriggerAll();

        await Assert.ThrowsAsync<OperationCanceledException>(() => syncTask);

    }

}