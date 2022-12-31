using Codevoid.Storyvoid.Sync;
using Microsoft.UI.Dispatching;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Implementation of the Sync event sink/source that marshalls events onto the
/// dispatcher for easy UI-interactions
/// </summary>
internal class DispatcherSyncEvents : IDatabaseSyncEventSink, IDatabaseSyncEventSource
{
    private readonly DispatcherQueue queue;
    internal DispatcherSyncEvents(DispatcherQueue queue) => this.queue = queue;

    /// <summary>
    /// Invokes the supplied operation on the dispatcher. If we're already on
    /// the dispatcher, we will execute it directly. Otherwise we'll queue it on
    /// our dispatcher queue for later execution.
    /// </summary>
    /// <param name="operation">Encapsulated operation to perform</param>
    private void PerformOnDispatcher(DispatcherQueueHandler operation)
    {
        if (this.queue.HasThreadAccess)
        {
            // If we're already on the right thread, we can just invoke the
            // operation directly.
            operation();
            return;
        }

        this.queue.TryEnqueue(operation);
    }

    /// <inheritdoc />
    public event EventHandler? SyncStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersEnded;

    /// <inheritdoc />
    public event EventHandler? ArticlesStarted;

    /// <inheritdoc />
    public event EventHandler? ArticlesEnded;

    /// <inheritdoc />
    public event EventHandler? SyncEnded;

    /// <inheritdoc />
    public void RaiseArticlesEnded()
    {
        var handler = this.ArticlesEnded;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public void RaiseArticlesStarted()
    {
        var handler = this.ArticlesStarted;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public void RaiseFoldersEnded()
    {
        var handler = this.FoldersEnded;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public void RaiseFoldersStarted()
    {
        var handler = this.FoldersStarted;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public void RaiseSyncEnded()
    {
        var handler = this.SyncEnded;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public void RaiseSyncStarted()
    {
        var handler = this.SyncStarted;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, EventArgs.Empty));
    }
}
