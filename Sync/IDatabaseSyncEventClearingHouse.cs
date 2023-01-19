namespace Codevoid.Storyvoid.Sync;

/// <summary>
/// Event sink from the syncing component for consumers to react to those
/// changes -- either for display to a user, or for logging & diagnostic
/// purposes.
/// 
/// It's important to note that it is up to the listener to ensure they handle
/// the event on the correct thread
/// </summary>
public interface IDatabaseSyncEventSink
{
    /// <summary>
    /// A sync has begin.
    /// </summary>
    event EventHandler SyncStarted;

    /// <summary>
    /// Syncing of the folders list (added, removed, changed) has begun
    /// </summary>
    event EventHandler FoldersStarted;

    /// <summary>
    /// Syncing of the folders list (added, removed, changed) has completed
    /// </summary>
    event EventHandler FoldersEnded;

    /// <summary>
    /// An error occured while syncing folders
    /// </summary>
    event EventHandler FoldersError;

    /// <summary>
    /// Syncing of articles, across all folders (added, removed, changed) has
    /// begun
    /// </summary>
    event EventHandler ArticlesStarted;

    /// <summary>
    /// Syncing of articles, across all folders (added, removed, changed) has
    /// completed
    /// </summary>
    event EventHandler ArticlesEnded;

    /// <summary>
    /// An error occured while syncing articles
    /// </summary>
    event EventHandler ArticlesError;

    /// <summary>
    /// A Sync has completed (Either successfully, or in error)
    /// </summary>
    event EventHandler SyncEnded;

    /// <summary>
    /// An error occured during the sync
    /// </summary>
    event EventHandler SyncError;
}

/// <summary>
/// Event source to raise events related to a sync process.
/// </summary>
public interface IDatabaseSyncEventSource
{
    /// <summary>
    /// Raise that syncing has begun
    /// </summary>
    void RaiseSyncStarted();

    /// <summary>
    /// Raise that syncing of folders has begun
    /// </summary>
    void RaiseFoldersStarted();

    /// <summary>
    /// Raise that syncing of folders has completed
    /// </summary>
    void RaiseFoldersEnded();

    /// <summary>
    /// Raise that an error occured while syncing folders
    /// </summary>
    void RaiseFoldersError();

    /// <summary>
    /// Raise that syncing of articles has begun
    /// </summary>
    void RaiseArticlesStarted();

    /// <summary>
    /// Raise that syncing of articles has completed
    /// </summary>
    void RaiseArticlesEnded();

    /// <summary>
    /// Raise that an error occured during the syncing of articles
    /// </summary>
    void RaiseArticlesError();

    /// <summary>
    /// Raise that the syncing has completed
    /// </summary>
    void RaiseSyncEnded();

    /// <summary>
    /// Raise that an error occured in syncing
    /// </summary>
    void RaiseSyncError();
}