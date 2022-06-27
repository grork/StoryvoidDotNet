namespace Codevoid.Storyvoid;

internal class Ledger : IDisposable
{
    private IFolderDatabase folderDatabase;
    private IFolderChangesDatabase folderChangesDatabase;
    private IArticleChangesDatabase articleChangesDatabase;

    internal Ledger(IFolderDatabase folderDatabase, IFolderChangesDatabase folderChangesDatabase, IArticleChangesDatabase articleChangesDatabase)
    {
        this.folderDatabase = folderDatabase;
        this.folderChangesDatabase = folderChangesDatabase;
        this.articleChangesDatabase = articleChangesDatabase;

        this.folderDatabase.FolderAdded += this.HandleFolderAdded;
        this.folderDatabase.FolderWillBeDeleted += this.HandleFolderWillBeDeleted;
        this.folderDatabase.FolderDeleted += this.HandleFolderDeleted;
    }

    private void HandleFolderAdded(object _, string title)
    {
        var added = this.folderDatabase.GetFolderByTitle(title)!;
        
        // If we have a pending folder delete for a folder with the same title
        // as one that the service is aware of, we need to resurrect the the
        // service-side details of that item. When we created a delete for that
        // folder, we saved off those details, so we can grab them from that.
        // Note, since we had a pending delete, there is no need to create a
        // pending add.
        var pendingDelete = this.folderChangesDatabase.GetPendingFolderDeleteByTitle(title);
        if(pendingDelete is not null)
        {
            this.folderChangesDatabase.DeletePendingFolderDelete(pendingDelete.ServiceId);
            this.folderDatabase.UpdateFolder(
                added.LocalId,
                pendingDelete.ServiceId,
                added.Title,
                added.Position,
                added.ShouldSync
            );
            return;
        }
        this.folderChangesDatabase.CreatePendingFolderAdd(added.LocalId);
    }

    private void HandleFolderWillBeDeleted(object _, DatabaseFolder toBeDeleted)
    {
        if(this.articleChangesDatabase.ListPendingArticleMovesForLocalFolderId(toBeDeleted.LocalId).Any())
        {
            throw new FolderHasPendingArticleMoveException(toBeDeleted.LocalId);
        }

        // Check for a pending folder add, and remove that pending operation
        // before we continue.
        var pendingAdd = this.folderChangesDatabase.GetPendingFolderAdd(toBeDeleted.LocalId);
        if(pendingAdd is not null)
        {
            this.folderChangesDatabase.DeletePendingFolderAdd(pendingAdd.FolderLocalId);
        }
    }

    private void HandleFolderDeleted(object _, DatabaseFolder deleted)
    {
        if(!deleted.ServiceId.HasValue)
        {
            // Folders without service id's have never been seen by the service
            // so don't need to track them
            return;
        }

        this.folderChangesDatabase.CreatePendingFolderDelete(
            deleted.ServiceId!.Value,
            deleted.Title);
    }

    public void Dispose()
    {
        this.folderDatabase.FolderAdded -= this.HandleFolderAdded;
    }
}