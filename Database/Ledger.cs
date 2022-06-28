namespace Codevoid.Storyvoid;

internal class Ledger : IDisposable
{
    private IFolderDatabaseWithTransactionEvents folderDatabase;
    private IFolderChangesDatabase folderChangesDatabase;
    private IArticleDatabaseWithTransactionEvents articleDatabase;
    private IArticleChangesDatabase articleChangesDatabase;

    internal Ledger(
        IFolderDatabaseWithTransactionEvents folderDatabase,
        IFolderChangesDatabase folderChangesDatabase,
        IArticleDatabaseWithTransactionEvents articleDatabase,
        IArticleChangesDatabase articleChangesDatabase)
    {
        this.folderDatabase = folderDatabase;
        this.folderChangesDatabase = folderChangesDatabase;
        this.articleDatabase = articleDatabase;
        this.articleChangesDatabase = articleChangesDatabase;

        this.folderDatabase.FolderAddedWithinTransaction += this.HandleFolderAdded;
        this.folderDatabase.FolderWillBeDeletedWithinTransaction += this.HandleFolderWillBeDeleted;
        this.folderDatabase.FolderDeletedWithinTransaction += this.HandleFolderDeleted;
        this.articleDatabase.ArticleDeletedWithinTransaction += this.HandleArticleDeleted;
        this.articleDatabase.ArticleLikeStatusChangedWithinTransaction += this.HandleArticleLikeStatusChanged;
        this.articleDatabase.ArticleMovedToFolderWithinTransaction += this.HandleArticleMovedToFolder;
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

        _ = this.folderChangesDatabase.CreatePendingFolderAdd(added.LocalId);
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

        _ = this.folderChangesDatabase.CreatePendingFolderDelete(
            deleted.ServiceId!.Value,
            deleted.Title);
    }

    private void HandleArticleDeleted(object _, long deletedId)
    {
        _ = this.articleChangesDatabase.CreatePendingArticleDelete(deletedId);
    }

    private void HandleArticleLikeStatusChanged(object _, DatabaseArticle article)
    {
        // Get any existing like status change...
        var existingStatusChange = this.articleChangesDatabase.GetPendingArticleStateChangeByArticleId(article.Id);
        if(existingStatusChange is not null)
        {
            // If it's opposite to our new state, we can just delete it.
            if(article.Liked != existingStatusChange.Liked)
            {
                this.articleChangesDatabase.DeletePendingArticleStateChange(article.Id);
            }

            // and since it's back to it's original state or the same as the
            // pending edit, we can give up.
            return;
        }

        // If there wasn't, create one for the new pending state
        _ = this.articleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
    }

    private void HandleArticleMovedToFolder(object _, (DatabaseArticle Article, long DestinationLocalFolderId) payload)
    {
        // Check to see if this article was already pending a move to a folder.
        // If so, we don't need it anymore, and it'll need to be cleaned up first
        var existingMove = this.articleChangesDatabase.GetPendingArticleMove(payload.Article.Id);
        if(existingMove is not null)
        {
            this.articleChangesDatabase.DeletePendingArticleMove(payload.Article.Id);
        }

        _ = this.articleChangesDatabase.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
    }

    public void Dispose()
    {
        this.folderDatabase.FolderAddedWithinTransaction -= this.HandleFolderAdded;
        this.folderDatabase.FolderWillBeDeletedWithinTransaction -= this.HandleFolderWillBeDeleted;
        this.folderDatabase.FolderDeletedWithinTransaction -= this.HandleFolderDeleted;
        this.articleDatabase.ArticleDeletedWithinTransaction -= this.HandleArticleDeleted;
        this.articleDatabase.ArticleLikeStatusChangedWithinTransaction -= this.HandleArticleLikeStatusChanged;
        this.articleDatabase.ArticleMovedToFolderWithinTransaction -= this.HandleArticleMovedToFolder;
    }
}