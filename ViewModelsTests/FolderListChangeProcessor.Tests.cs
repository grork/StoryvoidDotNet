using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class FolderListChangeProcessorTests
{
    private (DatabaseFolder Unread, DatabaseFolder Archive) defaultFolders = TestUtilities.GetMockDefaultFolders();
    private readonly DatabaseEventClearingHouse clearingHouse = new DatabaseEventClearingHouse();

    private IList<DatabaseFolder> GetDefaultFolderList()
    {
        var folders = new List<DatabaseFolder>
        {
            this.defaultFolders.Unread,
            this.defaultFolders.Archive,
            TestUtilities.GetMockSyncedDatabaseFolder()
        };

        folders.Sort(new FolderComparer());

        return folders;
    }

    [Fact]
    public void AddingDefaultUnreadFolderWithEmptyFolderListIsAdded()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        this.clearingHouse.RaiseFolderAdded(this.defaultFolders.Unread);

        Assert.Single(folders);
        Assert.Equal(this.defaultFolders.Unread, folders.First());
    }

    [Fact]
    public void AddingDefaultArchiveFolderWithEmptyFolderListIsAdded()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        this.clearingHouse.RaiseFolderAdded(this.defaultFolders.Archive);

        Assert.Single(folders);
        Assert.Equal(this.defaultFolders.Archive, folders.First());
    }

    [Fact]
    public void AddingSyncedFolderWithEmptyFolderListIsAdded()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Single(folders);
        Assert.Equal(folderToAdd, folders.First());
    }

    [Fact]
    public void AddingUnsyncedFolderWithEmptyFolderListIsAdded()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockUnsyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Single(folders);
        Assert.Equal(folderToAdd, folders.First());
    }

    [Fact]
    public void AddingSyncedFolderWithToDefaultListPlacesFolderLast()
    {
        var folders = new List<DatabaseFolder>
        {
            this.defaultFolders.Unread,
            this.defaultFolders.Archive
        };

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(3, folders.Count);
        Assert.Equal(folderToAdd, folders.Last());
    }

    [Fact]
    public void AddingSyncedFolderPlacesFolderLast()
    {
        var folders = this.GetDefaultFolderList();

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(4, folders.Count);
        Assert.Equal(folderToAdd, folders.Last());
    }

    [Fact]
    public void AddingUnsyncedFolderPlacesFolderLast()
    {
        var folders = this.GetDefaultFolderList();

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockUnsyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(4, folders.Count);
        Assert.Equal(folderToAdd, folders.Last());
    }

    [Fact]
    public void AddingSyncedFolderWithUnsyncedFoldersPlacesFolderBeforeUnsynced()
    {
        var folders = this.GetDefaultFolderList();

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        // Add an unsync'd folder before we add another sync'd
        this.clearingHouse.RaiseFolderAdded(TestUtilities.GetMockUnsyncedDatabaseFolder());

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();
        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(5, folders.Count);
        Assert.Equal(folderToAdd, folders[folders.Count - 2]);
    }
}

