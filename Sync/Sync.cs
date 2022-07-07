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

public class Sync
{
    private IFolderDatabase folderDb;
    private IFolderChangesDatabase folderChangesDb;
    private IFoldersClient foldersClient;

    public Sync(IFolderDatabase folderDb,
                IFolderChangesDatabase folderChangesDb,
                IFoldersClient foldersClient)
    {
        this.folderDb = folderDb;
        this.folderChangesDb = folderChangesDb;
        this.foldersClient = foldersClient;
    }

    public async Task SyncFolders()
    {
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
}