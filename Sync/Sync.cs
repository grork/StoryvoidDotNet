using Codevoid.Instapaper;

namespace Codevoid.Storyvoid;

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

    public int FolderCount
    {
        get
        {
            return this.folderDb.ListAllFolders().Count;
        }
    }
}