using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid;

internal sealed class SyncEventClearingHouse : IDatabaseSyncEventSink, IDatabaseSyncEventSource
{
    /// <inheritdoc />
    public event EventHandler? SyncStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersStarted;

    /// <inheritdoc />
    public event EventHandler? FoldersEnded;

    /// <inheritdoc />
    public event EventHandler? FoldersError;

    /// <inheritdoc />
    public event EventHandler? ArticlesStarted;

    /// <inheritdoc />
    public event EventHandler? ArticlesEnded;

    /// <inheritdoc />
    public event EventHandler? ArticlesError;

    /// <inheritdoc />
    public event EventHandler? SyncEnded;

    /// <inheritdoc />
    public event EventHandler? SyncError;

    /// <inheritdoc />
    public void RaiseSyncStarted()
    {
        var handler = this.SyncStarted;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseFoldersStarted()
    {
        var handler = this.FoldersStarted;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseFoldersEnded()
    {
        var handler = this.FoldersEnded;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseFoldersError()
    {
        var handler = this.FoldersError;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseArticlesStarted()
    {
        var handler = this.ArticlesStarted;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseArticlesEnded()
    {
        var handler = this.ArticlesEnded;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseArticlesError()
    {
        var handler = this.ArticlesError;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseSyncEnded()
    {
        var handler = this.SyncEnded;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RaiseSyncError()
    {
        var handler = this.SyncError;
        handler?.Invoke(this, EventArgs.Empty);
    }
}