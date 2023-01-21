using Codevoid.Storyvoid.Sync;
using Microsoft.UI.Dispatching;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Implementation of the Sync event sink/source that marshalls events onto the
/// dispatcher for easy UI-interactions
/// </summary>
internal class DispatcherSyncEvents : EventDispatcherBase, IDatabaseSyncEventSink, IDatabaseSyncEventSource
{
    internal DispatcherSyncEvents(DispatcherQueue queue) : base(queue)
    { }

    /// <inheritdoc />
    public event EventHandler? SyncStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersEnded;

    /// <inheritdoc/>
    public event EventHandler<Exception?>? FoldersError;

    /// <inheritdoc />
    public event EventHandler? ArticlesStarted;

    /// <inheritdoc />
    public event EventHandler? ArticlesEnded;

    /// <inheritdoc/>
    public event EventHandler<Exception?>? ArticlesError;

    /// <inheritdoc />
    public event EventHandler? SyncEnded;

    public event EventHandler<Exception?>? SyncError;

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

    /// <inheritdoc/>
    public void RaiseArticlesError(Exception? exception)
    {
        var handler = this.ArticlesError;
        if(handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, exception));
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

    public void RaiseFoldersError(Exception? exception)
    {
        var handler = this.FoldersError;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, exception));
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

    public void RaiseSyncError(Exception? exception)
    {
        var handler = this.SyncError;
        if (handler is null)
        {
            return;
        }

        this.PerformOnDispatcher(() => handler.Invoke(this, exception));
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
