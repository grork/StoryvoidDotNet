using Codevoid.Instapaper;
using System.Diagnostics;

namespace Codevoid.Storyvoid.Sync;

/// <summary>
/// Extensions to simplify working with the folders database in sync scenarios.
/// 
/// Most of these are contained here because sync is the interface between the
/// two worlds of the service, and the database.
/// </summary>
internal static class FolderDatabaseExtensions
{
    /// <summary>
    /// Locaate all the folders in the database that were created by a user (e.g
    /// not unread or archive)
    /// </summary>
    /// <returns>All the folders that are not the unread or archive folder</returns>
    internal static IList<DatabaseFolder> ListAllUserFolders(this IFolderDatabase instance)
    {
        return new List<DatabaseFolder>(from f in instance.ListAllFolders()
                                        where f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                                        select f);
    }

    /// <summary>
    /// Creates a folder in the database with the server information. These are
    /// fully known folders, so have all information available.
    /// </summary>
    /// <param name="toAdd">Folder to add</param>
    /// <returns>Created <see cref="DatabaseFolder">folder</see></returns>
    internal static DatabaseFolder AddKnownFolder(this IFolderDatabase instance, IInstapaperFolder toAdd)
    {
        return instance.AddKnownFolder(
            title: toAdd.Title,
            serviceId: toAdd.Id,
            position: toAdd.Position,
            shouldSync: toAdd.SyncToMobile);
    }

    /// <summary>
    /// Get a service-facing folder ID (E.g. a string) from the database folder.
    /// 
    /// The service doesn't use numeric IDs for all folders -- unread, archive,
    /// and liked use alphanumeric IDs (aka "unread"), so we need to handle
    /// those cases differently, rather than a blind "ToString" on the ServiceId
    /// </summary>
    /// <returns>A service-compatible ID for the folder requested</returns>
    internal static string GetServiceCompatibleFolderId(this DatabaseFolder instance)
    {
        switch (instance.ServiceId)
        {
            case WellKnownServiceFolderIds.Unread:
                return WellKnownFolderIds.Unread;

            case WellKnownServiceFolderIds.Archive:
                return WellKnownFolderIds.Archived;

            default:
                return instance.ServiceId.ToString();
        }
    }
}

/// <summary>
/// Extensions to simplify working with the articles database in sync scenarios.
/// 
/// Most of these are contained here because sync is the interface between the
/// two worlds of the service, and the database.
/// </summary>
internal static class ArticleDatabaseExtensions
{
    /// <summary>
    /// Converts a <see cref="IInstapaperBookmark">IInstapaperBookmark</see> to
    /// the data required for insertion into the database.
    /// </summary>
    /// <returns>Article information representation of the bookmark</returns>
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

    /// <summary>
    /// Conviencence method to convert a collection of <see cref="DatabaseArticle">
    /// DatabaseArticle</see> into <see cref="HaveStatus">HaveStatus</see> used
    /// when listing folder contents.
    /// </summary>
    /// <param name="instance"></param>
    /// <returns>Collection of HaveStatus for the articles supplied</returns>
    internal static IEnumerable<HaveStatus> HavesForArticles(this IEnumerable<DatabaseArticle> instance)
    {
        // Generate the have information based on the supplied articles
        var haveForArticles = instance.Select((a) => new HaveStatus(a.Id, a.Hash, a.ReadProgress, a.ReadProgressTimestamp));
        return haveForArticles;
    }
}

/// <summary>
/// Synchronizes the data in the supplied databases to the supplied Instapaper
/// service instances. This will extra pending changes, applying them to the
/// service, as well as enumerating service changes that need to be applied
/// locally.
/// </summary>
public class InstapaperSync
{
    /// <summary>
    /// The number of articles per folder to sync. This will limit the total
    /// articles in each folder. Liked articles that are outside the containing
    /// folders limit may still be sync'd as part of the liked article syncing.
    /// </summary>
    public uint ArticlesPerFolderToSync { get; set; } = 25;

    private IFolderDatabase folderDb;
    private IFolderChangesDatabase folderChangesDb;
    private IFoldersClient foldersClient;

    private IArticleDatabase articleDb;
    private IArticleChangesDatabase articleChangesDb;
    private IBookmarksClient bookmarksClient;

    public InstapaperSync(IFolderDatabase folderDb,
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

    public async Task SyncEverything()
    {
        await this.SyncFolders();
        await this.SyncBookmarks();
        this.CleanupOrphanedArticles();
    }

    /// <summary>
    /// Synchronises the folder information with the service. Pending adds &
    /// deletes are applied first before a 'mop up' of all folder information
    /// </summary>
    internal async Task SyncFolders()
    {
        await this.SyncPendingFolderAdds();
        await this.SyncPendingFolderDeletes();

        var remoteFoldersTask = this.foldersClient.ListAsync();
        var localFolders = this.folderDb.ListAllUserFolders();

        // Filter out folders that are not set to sync -- we won't add any that
        // aren't supposed to sync. If they were seen in an earlier sync and are
        // now set not to sync, they'll be cleaned up as not being available.
        var remoteFolders = (await remoteFoldersTask).Where((f) => f.SyncToMobile);

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
            await this.SyncSinglePendingFolderAdd(add);
        }
    }

    /// <summary>
    /// Syncs a single pending folder add to the service, and ensures the local
    /// database is updated with the now-available service information for that
    /// folder.
    /// 
    /// If a folder with the same title already exists on the service, the
    /// information for that folder will be applied locally.
    /// </summary>
    /// <param name="add">Information for the folder to add</param>
    /// <returns>Database folder if it was able to successfully sync</returns>
    private async Task<DatabaseFolder?> SyncSinglePendingFolderAdd(PendingFolderAdd add)
    {
        IInstapaperFolder? newServiceData = null;
        try
        {
            newServiceData = await this.foldersClient.AddAsync(add.Title);
        }
        catch (DuplicateFolderException)
        {
            // Folder with this title already existed. Since there isn't a
            // way to get the folder by title from the service, we need to
            // list all the folders and then find it.
            var remoteFolders = await this.foldersClient.ListAsync();
            var folderWithTitle = remoteFolders.First((f) => f.Title == add.Title);
            if (folderWithTitle is null)
            {
                Debug.Fail("Service informed we had a duplicate title, but couldn't actually find it");
            }

            newServiceData = folderWithTitle;
        }

        if (newServiceData is null)
        {
            return null;
        }

        var result = this.folderDb.UpdateFolder(
            localId: add.FolderLocalId,
            title: newServiceData.Title,
            serviceId: newServiceData.Id,
            position: newServiceData.Position,
            shouldSync: newServiceData.SyncToMobile
        );

        this.folderChangesDb.DeletePendingFolderAdd(add.FolderLocalId);

        return result;
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

    /// <summary>
    /// Syncs all information related to bookmarks to the service - pending add,
    /// delete, moves, and like status changes - as well as pulling service
    /// updates locally.
    /// 
    /// It is expected - but not required - that <see cref="SyncFolders">
    /// SyncFolders</see> will be executed before this.
    /// </summary>
    internal async Task SyncBookmarks()
    {
        await this.SyncPendingBookmarkAdds();
        await this.SyncPendingBookmarkDeletes();
        await this.SyncPendingBookmarkMoves();
        await this.SyncBookmarkStateByFolder();
        await this.SyncBookmarkLikeStatuses();
    }

    private async Task SyncPendingBookmarkAdds()
    {
        var adds = this.articleChangesDb.ListPendingArticleAdds();
        foreach(var add in adds)
        {
            _ = await this.bookmarksClient.AddAsync(add.Url, null);
            this.articleChangesDb.DeletePendingArticleAdd(add.Url);
        }
    }

    private async Task SyncPendingBookmarkDeletes()
    {
        var deletes = this.articleChangesDb.ListPendingArticleDeletes();
        foreach(var delete in deletes)
        {
            await this.bookmarksClient.DeleteAsync(delete);
            this.articleChangesDb.DeletePendingArticleDelete(delete);
        }
    }

    /// <summary>
    /// Applies all pending bookmark moves between folders we have locally to
    /// the service. If there is a move to a folder that has not been synced to
    /// the server (E.g. local folder add), that will be explicitly sync'd first.
    ///
    /// This does *not* discover/sync moves that have happened on the service,
    /// which happens in <see
    /// cref="SyncBookmarkStateByFolder>SyncBookmarkStateByFolder</see>.
    /// </summary>
    internal async Task SyncPendingBookmarkMoves()
    {
        var moves = this.articleChangesDb.ListPendingArticleMoves();
        foreach(var move in moves)
        {
            var destinationFolder = this.folderDb.GetFolderByLocalId(move.DestinationFolderLocalId);
            
            // Get our folder ducks in a row -- which may require us to sync a
            // pending folder add 'manually'.
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
                // anything with it directly, but we *can* sync that single 
                // add. First, find the add:
                var pendingFolderAdd = this.folderChangesDb.GetPendingFolderAdd(destinationFolder.LocalId);
                if(pendingFolderAdd is null)
                {
                    // If it's null, something has gone very bad; lets just sync
                    // ignore it, and hope it gets sorted out elsewhere
                    Debug.Fail("A pending folder add was not found for a folder without a service ID");
                    continue;
                }

                var demandSyncedFolder = await this.SyncSinglePendingFolderAdd(pendingFolderAdd);
                if(demandSyncedFolder is null)
                {
                    // Something went weirdly wrong, and we couldn't add the
                    // folder. So, give up on this move for now
                    continue;
                }

                destinationFolder = demandSyncedFolder;
            }

            // Perform the 'move' in the right way for the destination folders.
            // For 'unread', and 'archive', you can't 'move' an article into
            // that folder with a Move request -- it has to be 'added' (unread)
            // or 'archived' (archive) requests.
            IInstapaperBookmark? updatedBookmark = null;
            try
            {
                switch (destinationFolder.ServiceId)
                {
                    case WellKnownServiceFolderIds.Unread:
                        var existingArticle = this.articleDb.GetArticleById(move.ArticleId);
                        
                        // It shouldn't be possible for the article to be
                        // missing *locally*, due to the foreign-key relationship
                        Debug.Assert(existingArticle is not null, "Article to move to unread was missing in the local database");
                        if (existingArticle is not null)
                        {
                            updatedBookmark = await this.bookmarksClient.AddAsync(existingArticle.Url);
                        }
                        break;

                    case WellKnownServiceFolderIds.Archive:
                        updatedBookmark = await this.bookmarksClient.ArchiveAsync(move.ArticleId);
                        break;

                    default:
                        updatedBookmark = await this.bookmarksClient.MoveAsync(move.ArticleId, destinationFolder.ServiceId!.Value);
                        break;
                }
            }
            catch (EntityNotFoundException)
            {
                // Either the folder, or article is missing. If it's the
                // folder, maybe the article will show up else where. If it
                // has been deleted, then it will eventually become orphaned.
                // But, since it's not able to go where we thought it should go
                // lets orphan it locally -- future sync's will move it
                // somewhere safe if it's meant to be somewhere safe
                this.articleDb.RemoveArticleFromAnyFolder(move.ArticleId);
            }

            // Clean up the pending change, since we're complete making service
            // changes
            this.articleChangesDb.DeletePendingArticleMove(move.ArticleId);

            // If we were successful in getting updated information from the
            // service, we need to apply those changes locally.
            if(updatedBookmark is not null)
            {
                // IDs the same mean we can just update the the local store
                if (updatedBookmark.Id == move.ArticleId)
                {
                    this.articleDb.UpdateArticle(updatedBookmark.ToArticleRecordInformation());
                }
                else
                {
                    // A corner case of having a *local* move-to-unread, when
                    // the article has been deleted *remotely* creates a
                    // scenario where the article is forcefully re-added,
                    // resulting in a different article ID.
                    //
                    // This is because move-to-unread isn't handled by a *move*
                    // on the service, but in fact an add. This means if it had
                    // been deleted on the service *we don't know about it* and
                    // just end up adding it anyway. But this is ultimately a
                    // different article. So, we gotta delete the one we thought
                    // we were moving, and instead add a brand knew one.
                    this.articleDb.DeleteArticle(move.ArticleId);
                    this.articleDb.AddArticleToFolder(
                        updatedBookmark.ToArticleRecordInformation(),
                        move.DestinationFolderLocalId
                    );
                }
            }
        }
    }

    internal async Task SyncBookmarkLikeStatuses()
    {
        await SyncPendingBookmarkLikeStatusChanges();
        await SyncBookmarkLikedArticlesWithService();
    }

    /// <summary>
    /// Mops up any like status changes that happened on the service, which are
    /// blind to us. We need to list all the likes (with a List request), and
    /// apply locally. This is very similar to syncing the contents of a folder,
    /// but simplified in the handling of the results.
    /// </summary>
    private async Task SyncBookmarkLikedArticlesWithService()
    {
        var currentLikes = this.articleDb.ListLikedArticles();
        var (addedLikes, removedLiked) = await this.bookmarksClient.ListAsync(
            WellKnownFolderIds.Liked,
            currentLikes.HavesForArticles(),
            this.ArticlesPerFolderToSync
        );

        foreach (var unliked in removedLiked)
        {
            // If an article not liked any more, we can only toggle it's state
            // if it's present. Since we don't know if the article is *supposed*
            // to be in another folder, we need to allow other processes & parts
            // of sync to clean it up (e.g. delete it) if it's truly gone
            var existingArticle = this.articleDb.GetArticleById(unliked);
            if (existingArticle is not null)
            {
                this.articleDb.UnlikeArticle(unliked);
            }
        }

        foreach (var liked in addedLikes)
        {
            var existingArticle = this.articleDb.GetArticleById(liked.Id);
            if (existingArticle is not null)
            {
                this.articleDb.LikeArticle(liked.Id);
            }
            else
            {
                // Article was liked, but we didn't have it. This might mean
                // it's in the window of 'not in a folder, but in the liked sync
                // limit', and we need to add it to the database. But we don't
                // know where it's going to go -- we just have a like, no folder
                // association -- so we're just going to slap it in no-folder
                // land and let the clean up process decide if it should be kept
                this.articleDb.AddArticleNoFolder(liked.ToArticleRecordInformation());
            }
        }
    }

    private async Task SyncPendingBookmarkLikeStatusChanges()
    {
        var statusChanges = this.articleChangesDb.ListPendingArticleStateChanges();
        foreach (var stateChange in statusChanges)
        {
            IInstapaperBookmark? updatedBookmark = null;
            try
            {
                if (stateChange.Liked)
                {
                    updatedBookmark = await this.bookmarksClient.LikeAsync(stateChange.ArticleId);
                }
                else
                {
                    updatedBookmark = await this.bookmarksClient.UnlikeAsync(stateChange.ArticleId);
                }
            }
            catch (EntityNotFoundException)
            {
                // Bookmark wasn't on the service, so we can't do anything. We
                // assume that some other part of the sync process will
                // eventually clean up the item up, so for now, drop it.
            }

            this.articleChangesDb.DeletePendingArticleStateChange(stateChange.ArticleId);

            // If the article was missing remotely, we don't be able to update
            // it locally. It's assumed that the orphan clean up process will
            // appropriately clean this up
            if (updatedBookmark is not null)
            {
                this.articleDb.UpdateArticle(updatedBookmark.ToArticleRecordInformation());
            }
        }
    }

    /// <summary>
    /// Folder-by-folder, ask the service to tell us what is different, and
    /// apply those changes locally.
    /// </summary>
    private async Task SyncBookmarkStateByFolder()
    {
        // For ever service-sync'd folder, perform a sync
        var localFolders = this.folderDb.ListAllFolders();
        foreach(var folder in localFolders)
        {
            if(!folder.ServiceId.HasValue)
            {
                // We assume that folder sync or pending move sync will handle
                // new-to-the-service folders
                continue;
            }

            var articles = this.articleDb.ListArticlesForLocalFolder(folder.LocalId);
            await this.SyncBookmarksForFolder(articles, folder.GetServiceCompatibleFolderId(), folder.LocalId);
        }
    }

    /// <summary>
    /// For the supplied list of articles in a folder, sync with the service
    /// so it can tell us whats changed about whats in, out, and changed.
    /// </summary>
    /// <param name="articlesInFolder">List of articles we think are in the folder</param>
    /// <param name="folderServiceId">Folder Service ID to sync for (e.g. unread, archive, service ID)</param>
    /// <param name="localFolderId">Folder ID in the local database for that folder</param>
    private async Task SyncBookmarksForFolder(IEnumerable<DatabaseArticle> articlesInFolder, string folderServiceId, long localFolderId)
    {
        // Default to something so we don't have to check for nulls
        IList<IInstapaperBookmark> updates = new List<IInstapaperBookmark>();
        IList<long> deletes = new List<long>();

        try
        {
            (updates, deletes) = await this.bookmarksClient.ListAsync(folderServiceId, articlesInFolder.HavesForArticles(), this.ArticlesPerFolderToSync);
        }
        catch(EntityNotFoundException)
        {
            // The folder we were trying to sync has gone AWOL. It's deletion
            // will be processed by other parts of sync, so for now, just ignore
            // this specific folder
            return;
        }

        foreach(var delete in deletes)
        {
            // We don't delete the artical yet, because it might be *moved* to
            // another location. At some point, 'orphaned' articles will be
            // cleaned up; but thats not our concern here.
            this.articleDb.RemoveArticleFromAnyFolder(delete);
        }

        // These are the articles that had changes, so we need to apply them. At
        // this point, if it wasn't in 'deletes' and it isn't in 'updates', the
        // articles status is unchanged
        foreach(var update in updates)
        {
            // Articles that weren't in the have list are included, and either
            // need to be added (they're net new), or updated + moved
            if(this.articleDb.GetArticleById(update.Id) is null)
            {
                this.articleDb.AddArticleToFolder(update.ToArticleRecordInformation(), localFolderId);
                continue;
            }

            // Just assume that move is a no-op if it's already in the folder
            this.articleDb.MoveArticleToFolder(update.Id, localFolderId);

            // Update the database information for the article
            this.articleDb.UpdateArticle(update.ToArticleRecordInformation());
        }
    }

    internal void CleanupOrphanedArticles()
    {
        // Get all our local liked articles, and if they're within the limit of
        // the per-folder sync, we'll remove them from the articles that aren't
        // in a folder. This will give us a list of articles that are no
        // referenced anywhere.
        var likedArticles = new HashSet<long>(this.articleDb.ListLikedArticles().Take(Convert.ToInt32(this.ArticlesPerFolderToSync)).Select((a) => a.Id));
        var articlesNotInAFolder = this.articleDb.ListArticlesNotInAFolder().Where((a) => !likedArticles.Contains(a.Id));

        foreach(var article in articlesNotInAFolder)
        {
            this.articleDb.DeleteArticle(article.Id);
        }
    }
}