using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderDatabaseTests : IAsyncLifetime
{
    private static void FoldersMatch(DatabaseFolder? folder1, DatabaseFolder? folder2)
    {
        Assert.NotNull(folder1);
        Assert.NotNull(folder2);

        Assert.Equal(folder1!.LocalId, folder2!.LocalId);
        Assert.Equal(folder1!.ServiceId, folder2!.ServiceId);
        Assert.Equal(folder1!.Title, folder2!.Title);
        Assert.Equal(folder1!.Position, folder2!.Position);
        Assert.Equal(folder1!.ShouldSync, folder2!.ShouldSync);
    }

    private IInstapaperDatabase? instapaperDb;
    private IFolderDatabase? db;

    public async Task InitializeAsync()
    {
        this.instapaperDb = await TestUtilities.GetDatabase();
        this.db = this.instapaperDb.FolderDatabase;
    }

    public Task DisposeAsync()
    {
        this.instapaperDb?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void DefaultFoldersAreCreated()
    {
        IList<DatabaseFolder> result = this.db!.ListAllFolders();
        Assert.Equal(2, result.Count);

        var unreadFolder = result.Where((f) => f.ServiceId == WellKnownServiceFolderIds.Unread).First()!;
        var archiveFolder = result.Where((f) => f.ServiceId == WellKnownServiceFolderIds.Archive).First()!;

        // Check the convenience IDs are correct
        Assert.Equal(unreadFolder.LocalId, WellKnownLocalFolderIds.Unread);
        Assert.Equal(archiveFolder.LocalId, WellKnownLocalFolderIds.Archive);
    }

    [Fact]
    public void DefaultFoldersAreSortedCorrectly()
    {
        IList<DatabaseFolder> result = this.db!.ListAllFolders();
        Assert.Equal(2, result.Count);

        var firstFolder = result[0];
        var secondFolder = result[1];

        // Check the convenience IDs are correct
        Assert.Equal(firstFolder.LocalId, WellKnownLocalFolderIds.Unread);
        Assert.Equal(secondFolder.LocalId, WellKnownLocalFolderIds.Archive);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByServiceId()
    {
        var folder = db!.GetFolderByServiceId(WellKnownServiceFolderIds.Unread);

        Assert.NotNull(folder);
        Assert.Equal("Home", folder!.Title);
        Assert.Equal(WellKnownServiceFolderIds.Unread, folder!.ServiceId);
        Assert.NotEqual(0L, folder!.LocalId);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByLocalId()
    {
        var folder = this.db!.GetFolderByServiceId(WellKnownServiceFolderIds.Unread);
        folder = this.db!.GetFolderByLocalId(folder!.LocalId);

        Assert.NotNull(folder);
        Assert.Equal("Home", folder!.Title);
        Assert.Equal(WellKnownServiceFolderIds.Unread, folder!.ServiceId);
    }

    [Fact]
    public void GettingFolderThatDoesntExistByServiceIdDoesntReturnAnything()
    {
        var folder = this.db!.GetFolderByServiceId(1);

        Assert.Null(folder);
    }

    [Fact]
    public void GettingFolderThatDoesntExistByLocalIdDoesntReturnAnything()
    {
        var folder = this.db!.GetFolderByLocalId(5);

        Assert.Null(folder);
    }

    [Fact]
    public void CanAddFolder()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.CreateFolder("Sample");
        Assert.Null(addedFolder.ServiceId);
        Assert.Equal("Sample", addedFolder.Title);
        Assert.NotEqual(0L, addedFolder.LocalId);

        // Request the folder explicitily, check it's data
        DatabaseFolder folder = (this.db!.GetFolderByLocalId(addedFolder.LocalId))!;
        FoldersMatch(addedFolder, folder);

        // Check it comes back when listing all folders
        var allFolders = this.db!.ListAllFolders();
        Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByTitle()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.CreateFolder("Sample");

        // Request the folder explicitily, check it's data
        DatabaseFolder folder = this.db!.GetFolderByTitle("Sample")!;
        Assert.Equal(addedFolder.LocalId, folder.LocalId);
    }

    [Fact]
    public void FolderAddedEventRaisedForSingleFolderAdd()
    {
        DatabaseFolder? eventPayload = null;

        this.db!.FolderAdded += (_, addedFolder) => eventPayload = addedFolder;

        var addedFolder = this.db!.CreateFolder("Sample");
        Assert.Equal(addedFolder, eventPayload);
    }

    [Fact]
    public void CanAddMultipleFolders()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.CreateFolder("Sample");
        Assert.Null(addedFolder.ServiceId);
        Assert.Equal("Sample", addedFolder.Title);
        Assert.NotEqual(0L, addedFolder.LocalId);

        var addedFolder2 = this.db!.CreateFolder("Sample2");
        Assert.Null(addedFolder2.ServiceId);
        Assert.Equal("Sample2", addedFolder2.Title);
        Assert.NotEqual(0L, addedFolder2.LocalId);

        // Check it comes back when listing all folders
        var allFolders = this.db!.ListAllFolders();
        Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
        Assert.Equal(4, allFolders.Count);
    }

    [Fact]
    public void FolderAddedEventRaisedForMultipleFolders()
    {
        var eventPayloads = new List<DatabaseFolder>();

        this.db!.FolderAdded += (_, addedFolder) => eventPayloads.Add(addedFolder);

        var firstFolder = this.db!.CreateFolder("Sample");
        var secondFolder = this.db!.CreateFolder("Sample2");

        Assert.Equal(2, eventPayloads.Count);
        Assert.Equal(firstFolder, eventPayloads[0]);
        Assert.Equal(secondFolder, eventPayloads[1]);
    }

    [Fact]
    public void AddingFolderWithDuplicateTitleFails()
    {
        // Create folder; then created it again, expecting it to fail
        _ = this.db!.CreateFolder("Sample");
        Assert.Throws<DuplicateNameException>(() => this.db!.CreateFolder("Sample"));

        // Check a spurious folder wasn't created
        var allFolders = this.db!.ListAllFolders();
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void FolderAddedEventNotRaisedWhenAddFails()
    {
        var eventPayloads = new List<DatabaseFolder>();

        this.db!.FolderAdded += (_, addedFolder) => eventPayloads.Add(addedFolder);

        var firstFolder = this.db!.CreateFolder("Sample");
        Assert.Throws<DuplicateNameException>(() => this.db!.CreateFolder("Sample"));

        Assert.Single(eventPayloads);
        Assert.Equal(firstFolder, eventPayloads[0]);
    }

    [Fact]
    public void CanAddFolderWithAllServiceInformation()
    {
        // Create folder; check results are returned
        DatabaseFolder addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        Assert.Equal(10L, addedFolder.ServiceId);
        Assert.Equal("Sample", addedFolder.Title);
        Assert.NotEqual(0L, addedFolder.LocalId);
        Assert.Equal(9L, addedFolder.Position);
        Assert.True(addedFolder.ShouldSync);

        // Request the folder explicitily, check it's data
        DatabaseFolder folder = (this.db!.GetFolderByLocalId(addedFolder.LocalId))!;
        FoldersMatch(addedFolder, folder);

        // Check it comes back when listing all folders
        var allFolders = this.db!.ListAllFolders();
        var folderFromList = allFolders.Where((f) => f.LocalId == addedFolder.LocalId).FirstOrDefault();
        FoldersMatch(addedFolder, folderFromList);
    }

    [Fact]
    public void FolderAddedEventRaisedWhenKnownFolderAdded()
    {
        DatabaseFolder? eventPayload = null;

        this.db!.FolderAdded += (_, addedFolder) => eventPayload = addedFolder;

        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        Assert.Equal(addedFolder, eventPayload);
    }

    [Fact]
    public void AddFlderDuplicateTitleUsingServiceInformationThrows()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        Assert.Throws<DuplicateNameException>(() => this.db!.AddKnownFolder(
            title: addedFolder.Title,
            serviceId: addedFolder.ServiceId!.Value,
            position: addedFolder.Position,
            shouldSync: addedFolder.ShouldSync
        ));

        // Check it comes back when listing all folders
        var allFolders = this.db!.ListAllFolders();
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void CanUpdateAddedFolderWithFullSetOfInformation()
    {
        // Create local only folder
        var folder = this.db!.CreateFolder("Sample");
        Assert.Null(folder.ServiceId);
        Assert.False(folder.IsOnService);

        // Update the local only folder with additional data
        DatabaseFolder updatedFolder = db.UpdateFolder(
            localId: folder.LocalId,
            serviceId: 9L,
            title: "Sample2",
            position: 999L,
            shouldSync: false
        );

        // Should be the same folder
        Assert.Equal(folder.LocalId, updatedFolder.LocalId);

        // Values we updated should be reflected
        Assert.NotNull(updatedFolder.ServiceId);
        Assert.Equal(9L, updatedFolder.ServiceId);
        Assert.True(updatedFolder.IsOnService);

        Assert.Equal("Sample2", updatedFolder.Title);
        Assert.Equal(999L, updatedFolder.Position);
        Assert.False(updatedFolder.ShouldSync);
    }

    [Fact]
    public void UpdatingFolderThatDoesntExistFails()
    {
        var preCount = this.db!.ListAllFolders().Count;
        Assert.Throws<FolderNotFoundException>(() =>
        {
            _ = db!.UpdateFolder(
                localId: 9,
                serviceId: 9L,
                title: "Sample2",
                position: 999L,
                shouldSync: false
            );
        });

        // Check there wasn't one created
        var postCount = this.db!.ListAllFolders().Count;
        Assert.Equal(preCount, postCount);
    }

    [Fact]
    public void CanDeleteEmptyFolder()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        this.db!.DeleteFolder(addedFolder.LocalId);

        // Verify folder is missing
        var folders = this.db!.ListAllFolders();
        Assert.Equal(2, folders.Count);
    }

    [Fact]
    public void FolderWillBeDeletedEventRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderToBeDeleted = null;
        this.db!.FolderWillBeDeleted += (_, folder) => folderToBeDeleted = folder;

        this.db!.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderToBeDeleted);
    }

    [Fact]
    public void FolderDeletedEventRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderDeleted = null;
        this.db!.FolderDeleted += (_, folder) => folderDeleted = folder;

        this.db!.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderDeleted);
    }

    [Fact]
    public void FolderWillBeAndDeletedEventsRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db!.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderToBeDeleted = null;
        DatabaseFolder? folderDeleted = null;
        this.db!.FolderWillBeDeleted += (_, folder) => folderToBeDeleted = folder;
        this.db!.FolderDeleted += (_, folder) => folderDeleted = folder;


        this.db!.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderToBeDeleted);
        Assert.Equal(addedFolder, folderDeleted);
        Assert.Equal(folderToBeDeleted, folderDeleted);
    }

    [Fact]
    public void DeletingMissingFolderNoOps()
    {
        this.db!.DeleteFolder(999);
    }

    [Fact]
    public void FolderWillBeDeletedEventNotRaisedWhenNoFolderToDelete()
    {
        var wasRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => wasRaised = true;
        this.db!.DeleteFolder(999);

        Assert.False(wasRaised);
    }

    [Fact]
    public void FolderDeletedEventNotRaisedWhenNoFolderToDelete()
    {
        var wasRaised = false;
        this.db!.FolderDeleted += (_, _) => wasRaised = true;
        this.db!.DeleteFolder(999);

        Assert.False(wasRaised);
    }

    [Fact]
    public void FolderWillBeAndDeletedEventsNotRaisedWhenNoFolderToDelete()
    {
        var willBeDeletedRaised = false;
        var deletedRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => willBeDeletedRaised = true;
        this.db!.FolderDeleted += (_, _) => deletedRaised = true;

        this.db!.DeleteFolder(999);

        Assert.False(willBeDeletedRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void DeletingUnreadFolderThrows()
    {
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Unread));
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseWillDeleteEvent()
    {
        var wasRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseDeletedEvent()
    {
        var wasRaised = false;
        this.db!.FolderDeleted += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseWillDeleteOrDeletedEvent()
    {
        var willDeleteRaised = false;
        var deletedRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => willDeleteRaised = true;
        this.db!.FolderDeleted += (_, _) => deletedRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(willDeleteRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void DeletingArchiveFolderThrows()
    {
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Archive));
    }

    [Fact]
    public void DeletingArchiveFolderDoesntRaiseWillDeleteEvent()
    {
        var wasRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Archive));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingArchiveFolderDoesntRaiseWillDeleteOrDeletedEvent()
    {
        var willDeleteRaised = false;
        var deletedRaised = false;
        this.db!.FolderWillBeDeleted += (_, _) => willDeleteRaised = true;
        this.db!.FolderDeleted += (_, _) => deletedRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db!.DeleteFolder(WellKnownLocalFolderIds.Archive));
        Assert.False(willDeleteRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void AddingFolderWithoutPositionReturnsAfterWellKnownFolders()
    {
        // Create folder
        var addedFolder = this.db!.CreateFolder("Sample");

        // Check that the folder order is correct
        var allFolders = this.db!.ListAllFolders();
        Assert.Equal(WellKnownLocalFolderIds.Unread, allFolders[0].LocalId);
        Assert.Equal(WellKnownLocalFolderIds.Archive, allFolders[1].LocalId);
        Assert.Equal(addedFolder.LocalId, allFolders[2].LocalId);
    }

    [Fact]
    public void FoldersWithPositionAreSortedCorrectly()
    {
        // Create two folders, with their position ordering them opposite
        // to insertion order.
        var firstAddedFolder = this.db!.CreateFolder("Sample 1 - Sorted Second");
        this.db!.UpdateFolder(firstAddedFolder.LocalId, firstAddedFolder.ServiceId, firstAddedFolder.Title, 99L, firstAddedFolder.ShouldSync);

        var secondAddedFolder = this.db!.CreateFolder("Sample 2 - Sorted First");
        this.db!.UpdateFolder(secondAddedFolder.LocalId, secondAddedFolder.ServiceId, secondAddedFolder.Title, 11L, secondAddedFolder.ShouldSync);

        var thirdAddedFolder = this.db!.CreateFolder("Sample 3 - No position, sorted between well known and explicit positions");

        // Check it comes back when listing all folders
        var allFolders = this.db!.ListAllFolders();

        // Check that the folder order is correct
        Assert.Equal(WellKnownLocalFolderIds.Unread, allFolders[0].LocalId);
        Assert.Equal(WellKnownLocalFolderIds.Archive, allFolders[1].LocalId);
        Assert.Equal(thirdAddedFolder.LocalId, allFolders[2].LocalId);
        Assert.Equal(secondAddedFolder.LocalId, allFolders[3].LocalId);
        Assert.Equal(firstAddedFolder.LocalId, allFolders[4].LocalId);
    }
}