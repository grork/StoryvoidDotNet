using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Storyvoid.Sync;

/// <summary>
/// Syncing is complicated with the need for an transactionally-isolated
/// database instance. We also want to aggregate both the database sync, with
/// the article downloads when performing a sync. This means we need something
/// to tie them together. This is this class.
/// </summary>
public class SyncHelper : INotifyPropertyChanged
{
    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Current token source for canceling an in-progress sync.
    /// </summary>
    private CancellationTokenSource? cts;

    /// <summary>
    /// An article downloader that is **not** tied to the database instance that
    /// is used for the sync.
    /// </summary>
    private IArticleDownloader downloader;

    /// <summary>
    /// Factory call back that provides an awaitable task that will provide the
    /// sync instance _and_ it's associated database connection. This is called
    /// everytime sync is initiated.
    /// 
    /// It's important to note that the database connection is **closed** once
    /// database syncing has completed.
    /// </summary>
    private Func<Task<(IInstapaperSync, IDbConnection)>> syncFactory;

    /// <summary>
    /// Instantate the helper with the provided downloader &amp; sync factory
    /// function
    /// </summary>
    /// <param name="downloader">
    /// Downloader instance; not connected to the database that sync uses
    /// </param>
    /// <param name="syncFactory">
    /// Factory function that returns a task which completes with an opened
    /// database connection, and sync instance.
    /// 
    /// The DB connection will be closed by this class when the sync that
    /// requested that instance is completed.
    /// </param>
    public SyncHelper(IArticleDownloader downloader, Func<Task<(IInstapaperSync, IDbConnection)>> syncFactory)
    {
        this.downloader = downloader;
        this.syncFactory = syncFactory;
    }

    private void RaiseIsSyncingChanged() => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsSyncing)));

    /// <summary>
    /// Is there a sync currently in progress. To monitor for changes, subscribe
    /// to <see cref="PropertyChanged" />
    /// </summary>
    public bool IsSyncing => this.cts != null;


    /// <summary>
    /// Initate a sync of the database _and_ download missing article bodies.
    /// The returned task will complete when the syncing completes or has been
    /// cancelled.
    /// </summary>
    /// <returns>Task that completes when syncing has completed</returns>
    public async Task SyncDatabaseAndArticles()
    {
        // Don't initiate another sync if we already have one in progress
        if (this.IsSyncing)
        {
            return;
        }

        Debug.Assert(this.cts == null);

        // Create the canellation source to allow this to be cancelled. This
        // will assign the cancellation token source so that it can be cancelled
        using (this.cts = new CancellationTokenSource())
        {
            // Inform listeners that the syncing state has changed
            this.RaiseIsSyncingChanged();
            try
            {
                // Run the sync operation **on another thread**. We don't want
                // to initiate this on the UI thread; the various dependenices
                // here will marshal their events onto the right thread. This
                // allows the sync itself to run off in another thread,
                // **including opening the database**.
                await Task.Run(async () =>
                {
                    // Get a sync engine + datatabase connection -- which
                    // should be isolated from the primary database.
                    var (sync, connection) = await this.syncFactory();
                    using (connection)
                    {
                        try
                        {
                            await sync.SyncEverythingAsync(cts.Token);
                        }
                        finally
                        {
                            connection.Close();
                        }
                    }

                    // If we successfully synced, immediately start downloading
                    // the article content that does not currently have any
                    // local state available.
                    await this.downloader.DownloadAllArticlesWithoutLocalStateAsync(cts.Token);
                });
            }
            finally
            {
                this.cts = null;
                this.RaiseIsSyncingChanged();
            }
        }
    }

    /// <summary>
    /// Cancels any currently inprogress syncs that were initiated on this
    /// instance. This will cause the sync operation to end at the next
    /// available checkpoint.
    /// 
    /// If there is not currently a sync in progress, this will method will
    /// no-op.
    /// </summary>
    public void Cancel()
    {
        this.cts?.Cancel();
    }
}