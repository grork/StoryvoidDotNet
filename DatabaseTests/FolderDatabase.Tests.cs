using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderDatabaseTests : IDisposable
{
    private IDbConnection connection;
    private IFolderDatabaseWithTransactionEvents db;

    public FolderDatabaseTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.db = new FolderDatabase(this.connection);
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    private DatabaseEventClearingHouse SwitchToEventingDatabase()
    {
        var clearingHouse = new DatabaseEventClearingHouse();
        this.db = new FolderDatabase(this.connection, clearingHouse);
        return clearingHouse;
    }

    [Fact]
    public void DefaultFoldersAreCreated()
    {
        IList<DatabaseFolder> result = this.db.ListAllFolders();
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
        IList<DatabaseFolder> result = this.db.ListAllFolders();
        Assert.Equal(2, result.Count);

        var firstFolder = result[0];
        var secondFolder = result[1];

        // Check the convenience IDs are correct
        Assert.Equal(firstFolder.LocalId, WellKnownLocalFolderIds.Unread);
        Assert.Equal(secondFolder.LocalId, WellKnownLocalFolderIds.Archive);
    }

    [Fact]
    public void DefaultFoldersAreNotReturnedWithUserFoldersWhenNoUserFolders()
    {
        var allFolders = this.db.ListAllUserFolders();
        Assert.Empty(allFolders);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByServiceId()
    {
        var folder = this.db.GetFolderByServiceId(WellKnownServiceFolderIds.Unread);

        Assert.NotNull(folder);
        Assert.Equal("Home", folder!.Title);
        Assert.Equal(WellKnownServiceFolderIds.Unread, folder!.ServiceId);
        Assert.NotEqual(0L, folder!.LocalId);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByLocalId()
    {
        var folder = this.db.GetFolderByServiceId(WellKnownServiceFolderIds.Unread);
        folder = this.db.GetFolderByLocalId(folder!.LocalId);

        Assert.NotNull(folder);
        Assert.Equal("Home", folder!.Title);
        Assert.Equal(WellKnownServiceFolderIds.Unread, folder!.ServiceId);
    }

    [Fact]
    public void GettingFolderThatDoesntExistByServiceIdDoesntReturnAnything()
    {
        var folder = this.db.GetFolderByServiceId(1);

        Assert.Null(folder);
    }

    [Fact]
    public void GettingFolderThatDoesntExistByLocalIdDoesntReturnAnything()
    {
        var folder = this.db.GetFolderByLocalId(5);

        Assert.Null(folder);
    }

    [Fact]
    public void CanAddFolder()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.CreateFolder("Sample");
        Assert.Null(addedFolder.ServiceId);
        Assert.Equal("Sample", addedFolder.Title);
        Assert.NotEqual(0L, addedFolder.LocalId);

        // Request the folder explicitily, check it's data
        DatabaseFolder folder = (this.db.GetFolderByLocalId(addedFolder.LocalId))!;
        Assert.Equal(addedFolder, folder);

        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllFolders();
        Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void CanGetSingleDefaultFolderByTitle()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.CreateFolder("Sample");

        // Request the folder explicitily, check it's data
        DatabaseFolder folder = this.db.GetFolderByTitle("Sample")!;
        Assert.Equal(addedFolder.LocalId, folder.LocalId);
    }

    [Fact]
    public void ListingUserFoldersDoesNotIncludeDefaultFolders()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.CreateFolder("Sample");
        
        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllUserFolders();
        Assert.Contains(addedFolder, allFolders);
        Assert.Equal(1, allFolders.Count);
    }

    [Fact]
    public void FolderAddedWithinTransactionEventRaisedForSingleFolderAdd()
    {
        string? eventPayload = null;

        this.db.FolderAddedWithinTransaction += (_, payload) => eventPayload = payload.Data;

        var addedFolder = this.db.CreateFolder("Sample");
        Assert.Equal(addedFolder.Title, eventPayload);
    }

    [Fact]
    public void FolderAddedClearingHouseEventRaisedForSingleFolderAdd()
    {
        var clearingHouse = this.SwitchToEventingDatabase();
        DatabaseFolder? eventPayload = null;

        clearingHouse.FolderAdded += (_, payload) => eventPayload = payload;

        var addedFolder = this.db.CreateFolder("Sample");
        Assert.Equal(addedFolder, eventPayload);
    }

    [Fact]
    public void CanAddMultipleFolders()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.CreateFolder("Sample");
        Assert.Null(addedFolder.ServiceId);
        Assert.Equal("Sample", addedFolder.Title);
        Assert.NotEqual(0L, addedFolder.LocalId);

        var addedFolder2 = this.db.CreateFolder("Sample2");
        Assert.Null(addedFolder2.ServiceId);
        Assert.Equal("Sample2", addedFolder2.Title);
        Assert.NotEqual(0L, addedFolder2.LocalId);

        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllFolders();
        Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
        Assert.Equal(4, allFolders.Count);
    }

    [Fact]
    public void FolderAddedWithinTransactionEventRaisedForMultipleFolders()
    {
        var eventPayloads = new List<string>();

        this.db.FolderAddedWithinTransaction += (_, payload) => eventPayloads.Add(payload.Data);

        var firstFolder = this.db.CreateFolder("Sample");
        var secondFolder = this.db.CreateFolder("Sample2");

        Assert.Equal(2, eventPayloads.Count);
        Assert.Equal(firstFolder.Title, eventPayloads[0]);
        Assert.Equal(secondFolder.Title, eventPayloads[1]);
    }

    [Fact]
    public void AddingFolderWithDuplicateTitleFails()
    {
        // Create folder; then created it again, expecting it to fail
        _ = this.db.CreateFolder("Sample");
        Assert.Throws<DuplicateNameException>(() => this.db.CreateFolder("Sample"));

        // Check a spurious folder wasn't created
        var allFolders = this.db.ListAllFolders();
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void FolderAddedWithinTransactionEventNotRaisedWhenAddFails()
    {
        var eventPayloads = new List<string>();

        this.db.FolderAddedWithinTransaction += (_, payload) => eventPayloads.Add(payload.Data);

        var firstFolder = this.db.CreateFolder("Sample");
        Assert.Throws<DuplicateNameException>(() => this.db.CreateFolder("Sample"));

        Assert.Single(eventPayloads);
        Assert.Equal(firstFolder.Title, eventPayloads[0]);
    }

    [Fact]
    public void FolderAddedClearingHouseEventNotRaisedWhenAddFails()
    {
        var clearingHouse = this.SwitchToEventingDatabase();
        var eventPayloads = new List<DatabaseFolder>();

        clearingHouse.FolderAdded += (_, payload) => eventPayloads.Add(payload);

        var firstFolder = this.db.CreateFolder("Sample");
        Assert.Throws<DuplicateNameException>(() => this.db.CreateFolder("Sample"));

        Assert.Single(eventPayloads);
        Assert.Equal(firstFolder, eventPayloads[0]);
    }

    [Fact]
    public void CanAddFolderWithAllServiceInformation()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
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
        DatabaseFolder folder = (this.db.GetFolderByLocalId(addedFolder.LocalId))!;
        Assert.Equal(addedFolder, folder);

        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllFolders();
        var folderFromList = allFolders.Where((f) => f.LocalId == addedFolder.LocalId).FirstOrDefault();
        Assert.Equal(addedFolder, folderFromList);
    }

    [Fact]
    public void FolderAddedClearingHouseEventRaisedWhenAddingKnownFolder()
    {
        var clearingHouse = this.SwitchToEventingDatabase();
        DatabaseFolder? eventPayload = null;

        clearingHouse.FolderAdded += (_, payload) => eventPayload = payload;

        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true);

        Assert.Equal(addedFolder, eventPayload);
    }

    [Fact]
    public void AddFolderDuplicateTitleUsingServiceInformationThrows()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        Assert.Throws<DuplicateNameException>(() => this.db.AddKnownFolder(
            title: addedFolder.Title,
            serviceId: addedFolder.ServiceId!.Value,
            position: addedFolder.Position,
            shouldSync: addedFolder.ShouldSync
        ));

        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllFolders();
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void CanUpdateAddedFolderWithFullSetOfInformation()
    {
        // Create local only folder
        var folder = this.db.CreateFolder("Sample");
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
    public void FolderUpdatedClearingHouseEventRaisedWhenUpdatingAFolder()
    {
        // Create local only folder
        var folderId = this.db.CreateFolder("Sample").LocalId;

        var clearingHouse = this.SwitchToEventingDatabase();
        DatabaseFolder? eventPayload = null;

        clearingHouse.FolderUpdated += (_, payload) => eventPayload = payload;

        // Update the local only folder with additional data
        var updatedFolder = db.UpdateFolder(
            folderId,
            serviceId: 9L,
            title: "Sample2",
            position: 999L,
            shouldSync: false
        );

        Assert.Equal(updatedFolder, eventPayload);
    }

    [Fact]
    public void UpdatingFolderThatDoesntExistFails()
    {
        var preCount = this.db.ListAllFolders().Count;
        Assert.Throws<FolderNotFoundException>(() =>
        {
            _ = this.db.UpdateFolder(
                localId: 9,
                serviceId: 9L,
                title: "Sample2",
                position: 999L,
                shouldSync: false
            );
        });

        // Check there wasn't one created
        var postCount = this.db.ListAllFolders().Count;
        Assert.Equal(preCount, postCount);
    }

    [Fact]
    public void FolderUpdatedClearingHouseEventNotRaisedWhenUpdatingAFolderThatDoesntExist()
    {
        var clearingHouse = this.SwitchToEventingDatabase();
        var eventRaised = false;

        clearingHouse.FolderUpdated += (_, _) => eventRaised = true;

        // Update the local only folder with additional data
        Assert.Throws<FolderNotFoundException>(() =>
        {
            _ = db.UpdateFolder(
                99L,
                serviceId: 9L,
                title: "Sample2",
                position: 999L,
                shouldSync: false
            );
        });


        Assert.False(eventRaised);
    }

    [Fact]
    public void CanDeleteEmptyFolder()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        this.db.DeleteFolder(addedFolder.LocalId);

        // Verify folder is missing
        var folders = this.db.ListAllFolders();
        Assert.Equal(2, folders.Count);
    }

    [Fact]
    public void DeletingFolderContainingArticleRemovesFolder()
    {
        var articleDb = new ArticleDatabase(this.connection);
        var customFolder = this.db.CreateFolder("Sample");

        _ = articleDb.AddArticleToFolder(TestUtilities.GetRandomArticle(), customFolder.LocalId);
        _ = articleDb.AddArticleToFolder(TestUtilities.GetRandomArticle(), customFolder.LocalId);

        this.db.DeleteFolder(customFolder.LocalId);
        var folders = this.db.ListAllFolders();
        Assert.DoesNotContain(folders, (f) => f.LocalId == customFolder.LocalId);
    }

    [Fact]
    public void FolderWillBeDeletedWithinTransactionEventRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderToBeDeleted = null;
        this.db.FolderWillBeDeletedWithinTransaction += (_, payload) => folderToBeDeleted = payload.Data;

        this.db.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderToBeDeleted);
    }

    [Fact]
    public void FolderDeletedWithinTransactionEventRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderDeleted = null;
        this.db.FolderDeletedWithinTransaction += (_, payload) => folderDeleted = payload.Data;

        this.db.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderDeleted);
    }

    [Fact]
    public void FolderDeletedClearingHouseEventRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        var clearingHouse = this.SwitchToEventingDatabase();

        DatabaseFolder? folderDeleted = null;
        clearingHouse.FolderDeleted += (_, payload) => folderDeleted = payload;

        this.db.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderDeleted);
    }

    [Fact]
    public void FolderWillBeAndDeletedWithinTransactionEventsRaised()
    {
        // Create folder; check results are returned
        var addedFolder = this.db.AddKnownFolder(
            title: "Sample",
            serviceId: 10L,
            position: 9L,
            shouldSync: true
        );

        DatabaseFolder? folderToBeDeleted = null;
        DatabaseFolder? folderDeleted = null;
        this.db.FolderWillBeDeletedWithinTransaction += (_, payload) => folderToBeDeleted = payload.Data;
        this.db.FolderDeletedWithinTransaction += (_, payload) => folderDeleted = payload.Data;


        this.db.DeleteFolder(addedFolder.LocalId);

        Assert.Equal(addedFolder, folderToBeDeleted);
        Assert.Equal(addedFolder, folderDeleted);
        Assert.Equal(folderToBeDeleted, folderDeleted);
    }

    [Fact]
    public void DeletingMissingFolderNoOps()
    {
        this.db.DeleteFolder(999);
    }

    [Fact]
    public void FolderWillBeDeletedWithinTransactionEventNotRaisedWhenNoFolderToDelete()
    {
        var wasRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => wasRaised = true;
        this.db.DeleteFolder(999);

        Assert.False(wasRaised);
    }

    [Fact]
    public void FolderDeletedWithinTransacationEventNotRaisedWhenNoFolderToDelete()
    {
        var wasRaised = false;
        this.db.FolderDeletedWithinTransaction += (_, _) => wasRaised = true;
        this.db.DeleteFolder(999);

        Assert.False(wasRaised);
    }

    [Fact]
    public void FolderDeletedClearingHouseEventNotRaisedWhenNoFolderToDelete()
    {
        var clearingHouse = this.SwitchToEventingDatabase();
        var wasRaised = false;
        clearingHouse.FolderDeleted += (_, _) => wasRaised = true;
        this.db.DeleteFolder(999);

        Assert.False(wasRaised);
    }

    [Fact]
    public void FolderWillBeAndDeletedWithinTransactionEventsNotRaisedWhenNoFolderToDelete()
    {
        var willBeDeletedRaised = false;
        var deletedRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => willBeDeletedRaised = true;
        this.db.FolderDeletedWithinTransaction += (_, _) => deletedRaised = true;

        this.db.DeleteFolder(999);

        Assert.False(willBeDeletedRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void DeletingUnreadFolderThrows()
    {
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Unread));
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseWillDeleteWithinTransacationEvent()
    {
        var wasRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseDeletedWithinTransactionEvent()
    {
        var wasRaised = false;
        this.db.FolderDeletedWithinTransaction += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingUnreadFolderDoesntRaiseWillDeleteOrDeletedWithinTransactionEvent()
    {
        var willDeleteRaised = false;
        var deletedRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => willDeleteRaised = true;
        this.db.FolderDeletedWithinTransaction += (_, _) => deletedRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Unread));
        Assert.False(willDeleteRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void DeletingArchiveFolderThrows()
    {
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Archive));
    }

    [Fact]
    public void DeletingArchiveFolderDoesntRaiseWillDeleteWithinTransactionEvent()
    {
        var wasRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => wasRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Archive));
        Assert.False(wasRaised);
    }

    [Fact]
    public void DeletingArchiveFolderDoesntRaiseWillDeleteOrDeletedWithinTransacationEvent()
    {
        var willDeleteRaised = false;
        var deletedRaised = false;
        this.db.FolderWillBeDeletedWithinTransaction += (_, _) => willDeleteRaised = true;
        this.db.FolderDeletedWithinTransaction += (_, _) => deletedRaised = true;
        Assert.Throws<InvalidOperationException>(() => this.db.DeleteFolder(WellKnownLocalFolderIds.Archive));
        Assert.False(willDeleteRaised);
        Assert.False(deletedRaised);
    }

    [Fact]
    public void AddingFolderWithoutPositionReturnsAfterWellKnownFolders()
    {
        // Create folder
        var addedFolder = this.db.CreateFolder("Sample");

        // Check that the folder order is correct
        var allFolders = this.db.ListAllFolders();
        Assert.Equal(WellKnownLocalFolderIds.Unread, allFolders[0].LocalId);
        Assert.Equal(WellKnownLocalFolderIds.Archive, allFolders[1].LocalId);
        Assert.Equal(addedFolder.LocalId, allFolders[2].LocalId);
    }

    [Fact]
    public void FoldersWithPositionAreSortedCorrectly()
    {
        // Create two folders, with their position ordering them opposite
        // to insertion order.
        var firstAddedFolder = this.db.CreateFolder("Sample 1 - Sorted Second");
        this.db.UpdateFolder(firstAddedFolder.LocalId, firstAddedFolder.ServiceId, firstAddedFolder.Title, 99L, firstAddedFolder.ShouldSync);

        var secondAddedFolder = this.db.CreateFolder("Sample 2 - Sorted First");
        this.db.UpdateFolder(secondAddedFolder.LocalId, secondAddedFolder.ServiceId, secondAddedFolder.Title, 11L, secondAddedFolder.ShouldSync);

        var thirdAddedFolder = this.db.CreateFolder("Sample 3 - No position, sorted between well known and explicit positions");

        // Check it comes back when listing all folders
        var allFolders = this.db.ListAllFolders();

        // Check that the folder order is correct
        Assert.Equal(WellKnownLocalFolderIds.Unread, allFolders[0].LocalId);
        Assert.Equal(WellKnownLocalFolderIds.Archive, allFolders[1].LocalId);
        Assert.Equal(thirdAddedFolder.LocalId, allFolders[2].LocalId);
        Assert.Equal(secondAddedFolder.LocalId, allFolders[3].LocalId);
        Assert.Equal(firstAddedFolder.LocalId, allFolders[4].LocalId);
    }
}