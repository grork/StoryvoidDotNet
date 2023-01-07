using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid.ViewModels;

internal class MockInstapaperSync : IInstapaperSync
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

internal class MockArticleDownloader : IArticleDownloader
{
    private TaskCompletionSource downloadAllWithoutState = new TaskCompletionSource();
    private TaskCompletionSource downloadAllArticles = new TaskCompletionSource();
    private TaskCompletionSource<DatabaseLocalOnlyArticleState?> downloadSingleArticleState = new TaskCompletionSource<DatabaseLocalOnlyArticleState?>();

    public Task DownloadAllArticlesWithoutLocalStateAsync(CancellationToken cancellationToken = default) => this.downloadAllWithoutState.Task;
    public Task<DatabaseLocalOnlyArticleState?> DownloadArticleAsync(DatabaseArticle article, CancellationToken cancellationToken = default) => this.downloadSingleArticleState.Task;
    public Task DownloadArticlesAsync(IEnumerable<DatabaseArticle> articles, CancellationToken cancellationToken = default) => this.downloadAllArticles.Task;

    public void TriggerAll()
    {
        this.downloadAllArticles.SetResult();
        this.downloadAllWithoutState.SetResult();
        this.downloadSingleArticleState.SetResult(null);
    }
}