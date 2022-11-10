using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class FolderComprarerTests
{
    private readonly IComparer<DatabaseFolder> comparer = new FolderComparer();
    private readonly (DatabaseFolder Unread, DatabaseFolder Archive) defaultFolders = TestUtilities.GetMockDefaultFolders();

    [Fact]
    public void UnreadSortsBeforeArchive()
    {
        var relativeOrder = this.comparer.Compare(
            this.defaultFolders.Archive,
            this.defaultFolders.Unread
        );
        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            this.defaultFolders.Unread,
            this.defaultFolders.Archive
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void UnreadToUnreadSortsTheSame()
    {
        var relativeOrder = this.comparer.Compare(
            this.defaultFolders.Unread,
            this.defaultFolders.Unread
        );

        Assert.Equal(0, relativeOrder);
    }

    [Fact]
    public void ArchiveToArchiveSortsTheSame()
    {
        var relativeOrder = this.comparer.Compare(
            this.defaultFolders.Archive,
            this.defaultFolders.Archive
        );

        Assert.Equal(0, relativeOrder);
    }

    [Fact]
    public void UnreadSortedFirstArchiveSecondWhenOnlyFolders()
    {
        var folders = new List<DatabaseFolder>
        {
            this.defaultFolders.Archive,
            this.defaultFolders.Unread
        };

        folders.Sort(this.comparer);

        Assert.Equal(this.defaultFolders.Unread, folders.First());
        Assert.Equal(this.defaultFolders.Archive, folders.Last());
    }

    [Fact]
    public void UnreadAlwaysSortsBeforeSyncedFolder()
    {
        var relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockSyncedDatabaseFolder(),
            this.defaultFolders.Unread
        );

        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            this.defaultFolders.Unread,
            TestUtilities.GetMockSyncedDatabaseFolder()
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void ArchiveAlwaysSortsBeforeSyncedFolder()
    {
        var relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockSyncedDatabaseFolder(),
            this.defaultFolders.Archive
        );

        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            this.defaultFolders.Archive,
            TestUtilities.GetMockSyncedDatabaseFolder()
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void UnreadAlwaysSortsBeforeUnsyncedFolder()
    {
        var relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            this.defaultFolders.Unread
        );

        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            this.defaultFolders.Unread,
            TestUtilities.GetMockUnsyncedDatabaseFolder()
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void ArchiveAlwaysSortsBeforeUnsyncedFolder()
    {
        var relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            this.defaultFolders.Archive
        );

        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            this.defaultFolders.Archive,
            TestUtilities.GetMockUnsyncedDatabaseFolder()
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void SyncedFolderSortsBeforeUnsyncedFolder()
    {
        var relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockUnsyncedDatabaseFolder(),
            TestUtilities.GetMockSyncedDatabaseFolder()
        );

        Assert.Equal(1, relativeOrder);

        relativeOrder = this.comparer.Compare(
            TestUtilities.GetMockSyncedDatabaseFolder(),
            TestUtilities.GetMockUnsyncedDatabaseFolder()
        );

        Assert.Equal(-1, relativeOrder);
    }

    [Fact]
    public void SameIDDifferentPositionSyncedSortsCorrectOrder()
    {
        var folder = TestUtilities.GetMockSyncedDatabaseFolder();
        var folderUpdatedPosition = folder with { Position = folder.Position - 1 };
        var relativeOrder = this.comparer.Compare(
            folder,
            folderUpdatedPosition
        );

        Assert.Equal(1, relativeOrder);
    }

    [Fact]
    public void SameIDSamePositionSyncedIsSamePosition()
    {
        var folder = TestUtilities.GetMockSyncedDatabaseFolder();
        var relativeOrder = this.comparer.Compare(
            folder,
            folder
        );

        Assert.Equal(0, relativeOrder);
    }

    [Fact]
    public void UnsyncedFoldersWithDifferentPositionsRespectRelativePosition()
    {
        var first = TestUtilities.GetMockUnsyncedDatabaseFolder();
        var second = TestUtilities.GetMockUnsyncedDatabaseFolder();
        first = first with { Position = second.Position + 10 };

        var relativeOrder = this.comparer.Compare(first, second);
        Assert.Equal(1, relativeOrder);
    }

    [Fact]
    public void SameIDDifferentPositionUnyncedSortsCorrectOrder()
    {
        var folder = TestUtilities.GetMockUnsyncedDatabaseFolder();
        var folderUpdatedPosition = folder with { Position = folder.Position - 1 };
        var relativeOrder = this.comparer.Compare(
            folder,
            folderUpdatedPosition
        );

        Assert.Equal(1, relativeOrder);
    }

    [Fact]
    public void SameIDSamePositionUnsyncedIsSamePosition()
    {
        var folder = TestUtilities.GetMockUnsyncedDatabaseFolder();
        var relativeOrder = this.comparer.Compare(
            folder,
            folder
        );

        Assert.Equal(0, relativeOrder);
    }

    [Fact]
    public void UnsyncedWithLowerPositionThanSyncedSortsAfterSynced()
    {
        var synced = TestUtilities.GetMockSyncedDatabaseFolder();
        var unsynced = TestUtilities.GetMockUnsyncedDatabaseFolder() with { Position = synced.Position - 10 };

        var relativeOrder = this.comparer.Compare(unsynced, synced);
        Assert.Equal(1, relativeOrder);
    }

    [Fact]
    public void FoldersSortUnreadArchiveSyncedSyncedUnsynced()
    {
        var syncedFolder1 = TestUtilities.GetMockSyncedDatabaseFolder();
        var syncedFolder2 = TestUtilities.GetMockSyncedDatabaseFolder();
        var unsyncedFolder1 = TestUtilities.GetMockUnsyncedDatabaseFolder();
        var unsyncedFolder2 = TestUtilities.GetMockUnsyncedDatabaseFolder();

        var folders = new List<DatabaseFolder>
        {
            unsyncedFolder2,
            syncedFolder1,
            this.defaultFolders.Archive,
            unsyncedFolder1,
            syncedFolder2,
            this.defaultFolders.Unread,
        };

        folders.Sort(this.comparer);

        Assert.Equal(this.defaultFolders.Unread, folders[0]);
        Assert.Equal(this.defaultFolders.Archive, folders[1]);
        Assert.Equal(syncedFolder1, folders[2]);
        Assert.Equal(syncedFolder2, folders[3]);
        Assert.Equal(unsyncedFolder1, folders[4]);
        Assert.Equal(unsyncedFolder2, folders[5]);
    }
}

