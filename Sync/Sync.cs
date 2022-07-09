using Codevoid.Instapaper;
using System.Diagnostics;

namespace Codevoid.Storyvoid;

internal static class FolderDatabaseExtensions
{
    internal static IList<DatabaseFolder> ListAllUserFolders(this IFolderDatabase instance)
    {
        return new List<DatabaseFolder>(from f in instance.ListAllFolders()
                                        where f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                                        select f);
    }

    internal static DatabaseFolder AddKnownFolder(this IFolderDatabase instance, IInstapaperFolder toAdd)
    {
        return instance.AddKnownFolder(
            title: toAdd.Title,
            serviceId: toAdd.Id,
            position: toAdd.Position,
            shouldSync: toAdd.SyncToMobile);
    }
}

internal static class ArticleDatabaseExtensions
{
    internal static ArticleRecordInformation ToArticleRecordInformation(this IInstapaperBookmark instance)
    {
        return new ArticleRecordInformation(
            id: instance.Id,
            title: instance.Title,
            url: instance.Url,
            description: instance.Description,
            readProgress: instance.Progress,
            readProgressTimestamp: instance.ProgressTimestamp,
            hash: instance.Hash,
            liked: instance.Liked
        );
    }
}

public class Sync
{
    private IFolderDatabase folderDb;
    private IFolderChangesDatabase folderChangesDb;
    private IFoldersClient foldersClient;

    private IArticleDatabase articleDb;
    private IArticleChangesDatabase articleChangesDb;
    private IBookmarksClient bookmarksClient;

    public Sync(IFolderDatabase folderDb,
                IFolderChangesDatabase folderChangesDb,
                IFoldersClient foldersClient,
                IArticleDatabase articleDb,
                IArticleChangesDatabase articleChangesDb,
                IBookmarksClient bookmarksClient)
    {
        this.folderDb = folderDb;
        this.folderChangesDb = folderChangesDb;
        this.foldersClient = foldersClient;

        this.articleDb = articleDb;
        this.articleChangesDb = articleChangesDb;
        this.bookmarksClient = bookmarksClient;
    }

    public async Task SyncFolders()
    {
        await this.SyncPendingFolderAdds();
        await this.SyncPendingFolderDeletes();

        var remoteFoldersTask = this.foldersClient.ListAsync();
        var localFolders = this.folderDb.ListAllUserFolders();

        var remoteFolders = await remoteFoldersTask;

        // Check which remote folders need to be added or updated locally
        foreach (var rf in remoteFolders)
        {
            // See if we have it locally by ID
            var lf = this.folderDb.GetFolderByServiceId(rf.Id);
            if (lf is null)
            {
                // We don't have this folder locally, so we should just add it
                _ = this.folderDb.AddKnownFolder(rf);
                continue;
            }

            if (rf.Title != lf.Title || rf.Position != lf.Position || rf.SyncToMobile != lf.ShouldSync)
            {
                // We *do* have the folder. We need to update the folder
                _ = this.folderDb.UpdateFolder(lf.LocalId, lf.ServiceId, rf.Title, rf.Position, rf.SyncToMobile);
                continue;
            }
        }

        // Check for any local folders that are no longer present so we can delete them
        foreach(var lf in localFolders)
        {
            Debug.Assert(lf.ServiceId.HasValue, "Expected all pending folders to be uploaded");
            if(remoteFolders.Any((rf) => lf.ServiceId == rf.Id))
            {
                // This folder is present in the remote folders, nothing to do
                continue;
            }

            this.folderDb.DeleteFolder(lf.LocalId);
        }
    }

    private async Task SyncPendingFolderAdds()
    {
        var pendingAdds = this.folderChangesDb.ListPendingFolderAdds();
        foreach(var add in pendingAdds)
        {
            IInstapaperFolder? newServiceData = null;
            try
            {
                newServiceData = await this.foldersClient.AddAsync(add.Title);
            }
            catch(DuplicateFolderException)
            {
                // Folder with this title already existed. Since there isn't a
                // way to get the folder by title from the service, we need to
                // list all the folders and then find it.
                var remoteFolders = await this.foldersClient.ListAsync();
                var folderWithTitle = remoteFolders.First((f) => f.Title == add.Title);
                if(folderWithTitle is null)
                {
                    Debug.Fail("Service informed we had a duplicate title, but couldn't actually find it");
                }

                newServiceData = folderWithTitle;
            }

            if (newServiceData is null)
            {
                continue;
            }
            
            this.folderDb.UpdateFolder(
                localId: add.FolderLocalId,
                title: newServiceData.Title,
                serviceId: newServiceData.Id,
                position: newServiceData.Position,
                shouldSync: newServiceData.SyncToMobile
            );

            this.folderChangesDb.DeletePendingFolderAdd(add.FolderLocalId);
        }
    }

    private async Task SyncPendingFolderDeletes()
    {
        var pendingDeletes = this.folderChangesDb.ListPendingFolderDeletes();
        foreach(var delete in pendingDeletes)
        {
            try
            {
                await this.foldersClient.DeleteAsync(delete.ServiceId);
            }
            catch (EntityNotFoundException)
            {
                // It's OK to catch this; we were trying to delete it it anyway
                // and it's already gone.
            }
            
            this.folderChangesDb.DeletePendingFolderDelete(delete.ServiceId);
        }
    }

    public async Task SyncBookmarks()
    {
        await this.SyncBookmarkAdds();
        await this.SyncBookmarkDeletes();
        await this.SyncBookmarkMoves();
    }

    private async Task SyncBookmarkAdds()
    {
        var adds = this.articleChangesDb.ListPendingArticleAdds();
        foreach(var add in adds)
        {
            _ = await this.bookmarksClient.AddAsync(add.Url, null);
            this.articleChangesDb.DeletePendingArticleAdd(add.Url);
        }
    }

    private async Task SyncBookmarkDeletes()
    {
        var deletes = this.articleChangesDb.ListPendingArticleDeletes();
        foreach(var delete in deletes)
        {
            await this.bookmarksClient.DeleteAsync(delete);
            this.articleChangesDb.DeletePendingArticleDelete(delete);
        }
    }

    private async Task SyncBookmarkMoves()
    {
        var moves = this.articleChangesDb.ListPendingArticleMoves();
        foreach(var move in moves)
        {
            var destinationFolder = this.folderDb.GetFolderByLocalId(move.DestinationFolderLocalId);
            
            if(destinationFolder is null)
            {
                // If we don't find the target folder (Shouldn't be possible due
                // to foreign-key relationships), lets delete the pending move
                // and just assume it'll get tidied up in some other location
                Debug.Fail("Destination folder of move is not in the database");
                this.articleChangesDb.DeletePendingArticleMove(move.ArticleId);
                continue;
            }

            if(!destinationFolder.ServiceId.HasValue)
            {
                // If the target folder hasn't been sync'd yet, we can't do
                // anything with it, so we're going to just move onto the next
                // move, and catch it at somepoint in the future
                continue;
            }

            IInstapaperBookmark? updatedBookmark = null;
            try
            {
                switch (destinationFolder.ServiceId)
                {
                    case WellKnownServiceFolderIds.Unread:
                        var existingArticle = this.articleDb.GetArticleById(move.ArticleId);
                        
                        // It shouldn't be possible for the article to be
                        // missing *locall*, due to the foreign-key relationship
                        Debug.Assert(existingArticle is not null, "Article to move to unread was missing in the local database  ");
                        if (existingArticle is not null)
                        {
                            updatedBookmark = await this.bookmarksClient.AddAsync(existingArticle.Url);
                        }
                        break;

                    case WellKnownServiceFolderIds.Archive:
                        updatedBookmark = await this.bookmarksClient.ArchiveAsync(move.ArticleId);
                        break;

                    default:
                        updatedBookmark = await this.bookmarksClient.MoveAsync(move.ArticleId, destinationFolder.ServiceId.Value);
                        break;
                }
            }
            catch (EntityNotFoundException)
            {
                // Either the folder, or article is missing. If it's the
                // folder, maybe the article will show up else where. If it
                // has been deleted, then it will eventually become orphaned
                // So, lets not do anything
            }

            if(updatedBookmark is not null)
            {
                this.articleDb.UpdateArticle(updatedBookmark.ToArticleRecordInformation());
            }

            this.articleChangesDb.DeletePendingArticleMove(move.ArticleId);
        }
    }
}