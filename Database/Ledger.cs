namespace Codevoid.Storyvoid;

internal class Ledger : IDisposable
{
    IList<EventCleanupHelper> cleanup = new List<EventCleanupHelper>();

    internal Ledger(
        IFolderDatabaseWithTransactionEvents folderDatabase,
        IArticleDatabaseWithTransactionEvents articleDatabase)
    {
        this.cleanup.Add(() =>
        {
            folderDatabase.FolderAddedWithinTransaction += this.HandleFolderAdded;
            folderDatabase.FolderWillBeDeletedWithinTransaction += this.HandleFolderWillBeDeleted;
            folderDatabase.FolderDeletedWithinTransaction += this.HandleFolderDeleted;
        },
        () =>
        {
            folderDatabase.FolderAddedWithinTransaction -= this.HandleFolderAdded;
            folderDatabase.FolderWillBeDeletedWithinTransaction -= this.HandleFolderWillBeDeleted;
            folderDatabase.FolderDeletedWithinTransaction -= this.HandleFolderDeleted;
        });

        this.cleanup.Add(() =>
        {
            articleDatabase.ArticleDeletedWithinTransaction += this.HandleArticleDeleted;
            articleDatabase.ArticleLikeStatusChangedWithinTransaction += this.HandleArticleLikeStatusChanged;
            articleDatabase.ArticleMovedToFolderWithinTransaction += this.HandleArticleMovedToFolder;
        }, () =>
        {
            articleDatabase.ArticleDeletedWithinTransaction -= this.HandleArticleDeleted;
            articleDatabase.ArticleLikeStatusChangedWithinTransaction -= this.HandleArticleLikeStatusChanged;
            articleDatabase.ArticleMovedToFolderWithinTransaction -= this.HandleArticleMovedToFolder;
        });
    }

    private void HandleFolderAdded(IFolderDatabase fdb, WithinTransactionArgs<string> args)
    {
        var folderDatabase = new FolderDatabase(() => args.Connection);
        var title = args.Data;
        var added = folderDatabase.GetFolderByTitle(title)!;
        var folderChangesDatabase = new FolderChanges(() => args.Connection);

        // If we have a pending folder delete for a folder with the same title
        // as one that the service is aware of, we need to resurrect the the
        // service-side details of that item. When we created a delete for that
        // folder, we saved off those details, so we can grab them from that.
        // Note, since we had a pending delete, there is no need to create a
        // pending add.
        var pendingDelete = folderChangesDatabase.GetPendingFolderDeleteByTitle(title);
        if (pendingDelete is not null)
        {
            folderChangesDatabase.DeletePendingFolderDelete(pendingDelete.ServiceId);
            folderDatabase.UpdateFolder(
                added.LocalId,
                pendingDelete.ServiceId,
                added.Title,
                added.Position,
                added.ShouldSync
            );

            return;
        }

        _ = folderChangesDatabase.CreatePendingFolderAdd(added.LocalId);
    }

    private void HandleFolderWillBeDeleted(IFolderDatabase fdb, WithinTransactionArgs<DatabaseFolder> args)
    {
        var toBeDeleted = args.Data;
        var articleChangesDatabase = new ArticleChanges(() => args.Connection);
        var folderChangesDatabase = new FolderChanges(() => args.Connection);

        if (articleChangesDatabase.ListPendingArticleMovesForLocalFolderId(toBeDeleted.LocalId).Any())
        {
            throw new FolderHasPendingArticleMoveException(toBeDeleted.LocalId);
        }

        // Check for a pending folder add, and remove that pending operation
        // before we continue.
        var pendingAdd = folderChangesDatabase.GetPendingFolderAdd(toBeDeleted.LocalId);
        if (pendingAdd is not null)
        {
            folderChangesDatabase.DeletePendingFolderAdd(pendingAdd.FolderLocalId);
        }
    }

    private void HandleFolderDeleted(IFolderDatabase fdb, WithinTransactionArgs<DatabaseFolder> args)
    {
        var folderChangesDatabase = new FolderChanges(() => args.Connection);
        var deleted = args.Data;
        if (!deleted.ServiceId.HasValue)
        {
            // Folders without service id's have never been seen by the service
            // so don't need to track them
            return;
        }

        _ = folderChangesDatabase.CreatePendingFolderDelete(
            deleted.ServiceId!.Value,
            deleted.Title);
    }

    private void HandleArticleDeleted(IArticleDatabase adb, WithinTransactionArgs<long> args)
    {
        _ = new ArticleChanges(() => args.Connection).CreatePendingArticleDelete(args.Data);
    }

    private void HandleArticleLikeStatusChanged(IArticleDatabase adb, WithinTransactionArgs<DatabaseArticle> args)
    {
        var articleChangesDatabase = new ArticleChanges(() => args.Connection);
        var article = args.Data;
        // Get any existing like status change...
        var existingStatusChange = articleChangesDatabase.GetPendingArticleStateChangeByArticleId(article.Id);
        if (existingStatusChange is not null)
        {
            // If it's opposite to our new state, we can just delete it.
            if (article.Liked != existingStatusChange.Liked)
            {
                articleChangesDatabase.DeletePendingArticleStateChange(article.Id);
            }

            // and since it's back to it's original state or the same as the
            // pending edit, we can give up.
            return;
        }

        // If there wasn't, create one for the new pending state
        _ = articleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
    }

    private void HandleArticleMovedToFolder(
        IArticleDatabase adb,
        WithinTransactionArgs<(DatabaseArticle Article, long DestinationLocalFolderId)> args)
    {
        var articleChangesDatabase = new ArticleChanges(() => args.Connection);
        var payload = args.Data;
        // Check to see if this article was already pending a move to a folder.
        // If so, we don't need it anymore, and it'll need to be cleaned up first
        var existingMove = articleChangesDatabase.GetPendingArticleMove(payload.Article.Id);
        if (existingMove is not null)
        {
            articleChangesDatabase.DeletePendingArticleMove(payload.Article.Id);
        }

        _ = articleChangesDatabase.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
    }

    public void Dispose()
    {
        this.cleanup.DetachHandlers();
    }
}