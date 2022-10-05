using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class FolderListChangeProcessorTests
{
    private (DatabaseFolder Unread, DatabaseFolder Archive) defaultFolders = TestUtilities.GetMockDefaultFolders();
    private readonly DatabaseEventClearingHouse clearingHouse = new DatabaseEventClearingHouse();

    private IList<DatabaseFolder> GetDefaultFolderList(IList<DatabaseFolder>? extraFolders = null)
    {
        var folders = new List<DatabaseFolder>
        {
            this.defaultFolders.Unread,
            this.defaultFolders.Archive,
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder()
        };

        if (extraFolders is not null)
        {
            folders.AddRange(extraFolders);
        }

        folders.Sort(new FolderComparer());

        return folders;
    }

    private IList<DatabaseFolder> GetDefaultUnsyncedFolderList(IList<DatabaseFolder>? extraFolders = null)
    {
        var folders = new List<DatabaseFolder>
        {
            this.defaultFolders.Unread,
            this.defaultFolders.Archive,
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder()
        };

        if (extraFolders is not null)
        {
            folders.AddRange(extraFolders);
        }

        folders.Sort(new FolderComparer());

        return folders;
    }

    #region Additions
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
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(originalCount + 1, folders.Count);
        Assert.Equal(folderToAdd, folders.Last());
    }

    [Fact]
    public void AddingUnsyncedFolderPlacesFolderLast()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToAdd = TestUtilities.GetMockUnsyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(originalCount + 1, folders.Count);
        Assert.Equal(folderToAdd, folders.Last());
    }

    [Fact]
    public void AddingSyncedFolderWithUnsyncedFoldersPlacesFolderBeforeUnsynced()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var unsyncedFolder = TestUtilities.GetMockUnsyncedDatabaseFolder();
        // Add an unsync'd folder before we add another sync'd
        this.clearingHouse.RaiseFolderAdded(unsyncedFolder);

        var folderToAdd = TestUtilities.GetMockSyncedDatabaseFolder();
        this.clearingHouse.RaiseFolderAdded(folderToAdd);

        Assert.Equal(originalCount + 2, folders.Count);
        var addedFolderIndex = folders.IndexOf(folderToAdd);
        var unsyncedFolderIndex = folders.IndexOf(unsyncedFolder);
        Assert.Equal(unsyncedFolderIndex - 1, addedFolderIndex);
    }
    #endregion

    #region Updates
    [Fact]
    public void UpdateSyncedFolderInSingleFolderList()
    {
        var folders = new List<DatabaseFolder> { TestUtilities.GetMockSyncedDatabaseFolder() };
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = folders.First() with { Position = folders.First().Position + 1 };

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Single(folders);
        Assert.Equal(folderToUpdate, folders.First());
    }

    [Fact]
    public void UpdateSyncedFolderWithEmptyList()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Empty(folders);
    }

    [Fact]
    public void UpdateSyncedFolderNotInList()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = TestUtilities.GetMockSyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.DoesNotContain(folderToUpdate, folders);
        Assert.Equal(originalCount, folders.Count);
    }

    [Fact]
    public void UpdateSyncedFolderOrderToEnd()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var maxPosition = folders.Max((f) => f.Position);
        var folderToUpdate = folders.First((f) => f.ServiceId > 0) with { Position = maxPosition + 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.Last());
    }

    [Fact]
    public void UpdateSyncedFolderOrderToStart()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var minPosition = folders.Min((f) => f.Position);
        var folderToUpdate = folders.First((f) => f.ServiceId > 0) with { Position = minPosition - 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders[2]);
    }

    [Fact]
    public void UpdateSyncedFolderOrderMiddleToMiddle()
    {
        var folders = this.GetDefaultFolderList(new List<DatabaseFolder>
        {
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
        });

        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = folders[folders.Count - 3] with { Position = folders[folders.Count - 3].Position - 10 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.NotEqual(folderToUpdate, folders[folders.Count - 3]);
    }

    [Fact]
    public void UpdateUnsyncedFolderWithEmptyList()
    {
        var folders = new List<DatabaseFolder>();
        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = TestUtilities.GetMockUnsyncedDatabaseFolder();

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Empty(folders);
    }

    [Fact]
    public void UpdateUnsyncedFolderToSyncedStart()
    {
        var unsyncedToMove = TestUtilities.GetMockUnsyncedDatabaseFolder() with { Position = -1 };
        var folders = this.GetDefaultFolderList(new List<DatabaseFolder>
        {
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            unsyncedToMove,
        });

        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = unsyncedToMove with { ServiceId = 100000 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders[2]);
    }

    [Fact]
    public void UpdateUnsyncedFolderToSyncedEnd()
    {
        var unsyncedToMove = TestUtilities.GetMockUnsyncedDatabaseFolder() with { Position = 10000 };
        var folders = this.GetDefaultFolderList(new List<DatabaseFolder>
        {
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder(),
            unsyncedToMove,
            TestUtilities.GetMockUnsyncedDatabaseFolder()
        });

        Assert.Equal(unsyncedToMove, folders.Last());

        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var folderToUpdate = unsyncedToMove with { ServiceId = 100000 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.Last((f) => f.IsOnService));
    }

    [Fact]
    public void UpdateUnsyncedFolderPositionFromStartToEndWithNoSyncedFolders()
    {
        var folders = this.GetDefaultUnsyncedFolderList(new List<DatabaseFolder>()
        {
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
        });
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var maxPosition = folders.Max((f) => f.Position);
        var folderToUpdate = folders.First((f) => !f.IsOnService) with { Position = maxPosition + 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.Last());
    }

    [Fact]
    public void UpdateUnsyncedFolderPositionFromEndToStartWithNoSyncedFolders()
    {
        var folders = this.GetDefaultUnsyncedFolderList(new List<DatabaseFolder>()
        {
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
        });
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var minPosition = folders.Min((f) => f.Position);
        var folderToUpdate = folders.First((f) => !f.IsOnService) with { Position = minPosition - 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.First((f) => !f.IsOnService));
    }

    [Fact]
    public void UpdateUnsyncedFolderPositionFromStartToEndWithSyncedFolders()
    {
        var folders = this.GetDefaultFolderList(new List<DatabaseFolder>()
        {
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
        });

        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var maxPosition = folders.Max((f) => f.Position);
        var folderToUpdate = folders.First((f) => !f.IsOnService) with { Position = maxPosition + 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.Last());
    }

    [Fact]
    public void UpdateUnsyncedFolderPositionFromEndToStartWithSyncedFolders()
    {
        var folders = this.GetDefaultFolderList(new List<DatabaseFolder>()
        {
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
        });

        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);

        var minPosition = folders.Min((f) => f.Position);
        var folderToUpdate = folders.Last((f) => !f.IsOnService) with { Position = minPosition - 1 };
        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Contains(folderToUpdate, folders);
        Assert.Equal(folderToUpdate, folders.First((f) => !f.IsOnService));
    }

    [Fact]
    public void UpdateFolderTitleDoesntImpactOrder()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);
        var folderToUpdate = folders.Last() with { Title = "AAAA" };

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Equal(folderToUpdate, folders.Last());
    }

    [Fact]
    public void UpdateFolderSyncToMobileFieldDoesntImpactOrder()
    {
        var folders = this.GetDefaultFolderList();
        var originalCount = folders.Count;

        using var changeHandler = new FolderListChangeProcessor(folders, this.clearingHouse);
        var folderToUpdate = folders.Last() with { ShouldSync = false };

        this.clearingHouse.RaiseFolderUpdated(folderToUpdate);

        Assert.Equal(folderToUpdate, folders.Last());
    }
    #endregion
}