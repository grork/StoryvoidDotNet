namespace Codevoid.Test.Storyvoid;

public sealed class FolderSyncTests : BaseSyncTest
{
    [Fact]
    public async Task SyncingEmptyDatabasesCreatesEmptyState()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
    }

    #region Service-only Folder changes sync
    [Fact]
    public async Task SyncOnEmptyDatabaseCreatesCorrectFolders()
    {
        this.SwitchToEmptyLocalDatabase();
        
        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.True(this.databases.FolderDB.ListAllCompleteUserFolders().Count > 0);
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
    }

    [Fact]
    public async Task FoldersTitleDifferentOnServiceAreUpdatedDuringSync()
    {
        // Pick a folder to update
        var firstLocalUserFolder = (this.databases.FolderDB.ListAllCompleteUserFolders().First())!;

        // Update the title on the *service*
        var remoteFolder = (this.databases.MockFolderService.FolderDB.GetFolderByServiceId(firstLocalUserFolder.ServiceId!.Value))!;
        remoteFolder = this.databases.MockFolderService.FolderDB.UpdateFolder(
            title: "New Title",
            localId: remoteFolder.LocalId,
            serviceId: remoteFolder.ServiceId,
            position: remoteFolder.Position,
            shouldSync: remoteFolder.ShouldSync
        );

        await this.syncEngine.SyncFolders();

        firstLocalUserFolder = (this.databases.FolderDB.GetFolderByServiceId(firstLocalUserFolder.ServiceId!.Value))!;
        Assert.Equal(remoteFolder, firstLocalUserFolder, new CompareFoldersIgnoringLocalId());
    }

    [Fact]
    public async Task CompleteFoldersThatAreOnlyLocalAreRemovedDuringSync()
    {
        var targetFolderCount = this.databases.FolderDB.ListAllCompleteUserFolders().Count - 1;

        // Create a folder that only exists remotely
        var remoteToDelete = (this.databases.MockFolderService.FolderDB.ListAllCompleteUserFolders().First())!;
        this.databases.MockFolderService.FolderDB.DeleteFolder(remoteToDelete.LocalId);

        await this.syncEngine.SyncFolders();

        var localFolders = this.databases.FolderDB.ListAllCompleteUserFolders();
        Assert.Equal(targetFolderCount, localFolders.Count);
        Assert.DoesNotContain(remoteToDelete, localFolders, new CompareFoldersIgnoringLocalId());
    }

    [Fact]
    public async Task FoldersAddedOnServiceAreAddedWhenLocalDatabaseIsntEmpty()
    {
        var remoteFolder = this.databases.MockFolderService.FolderDB.AddCompleteFolderToDb();
        var localFolderCount = this.databases.FolderDB.ListAllCompleteUserFolders().Count;

        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.Equal(localFolderCount + 1, this.databases.FolderDB.ListAllCompleteUserFolders().Count());
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
    }

    [Fact]
    public async Task AddedAndRemovedFoldersOnServiceAreCorrectlySynced()
    {
        var deletedRemoteFolder = (this.databases.MockFolderService.FolderDB.ListAllCompleteUserFolders().First())!;
        var addedRemoteFolder = this.databases.MockFolderService.FolderDB.AddCompleteFolderToDb();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
    }
    #endregion

    #region Local-Only Folder changes sync
    [Fact]
    public async Task SyncingPendingAddToEmptyServiceAddsRemoteFolder()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        // Create pending add on empty DB
        var ledger = this.GetLedger();
        var newFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check we can get that same folder, and it now has a service ID
        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        // Check state matches, and the pending changes are gone
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task SyncingPendingAddToExistingServiceAddsRemoteFolder()
    {
        var ledger = this.GetLedger();
        var newFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task MultiplePendingAddsToEmptyServiceAddsAllPendingEdits()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        var ledger = this.GetLedger();
        var firstNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var secondNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder 2").LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        var firstSyncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(firstNewFolderId);
        Assert.NotNull(firstSyncedNewFolder);
        Assert.True(firstSyncedNewFolder!.ServiceId.HasValue);

        var secondSyncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(firstNewFolderId);
        Assert.NotNull(secondSyncedNewFolder);
        Assert.True(secondSyncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task PendingAddWithDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        // Remove a folder from the service, but save it's title
        var firstSeviceFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;
        this.databases.FolderDB.DeleteFolder(firstSeviceFolder.LocalId);

        // Start the ledger, and create a pending edit
        var ledger = this.GetLedger();
        var newFolderId = this.databases.FolderDB.CreateFolder(firstSeviceFolder.Title).LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task MultipleAddsWithWithOneDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        var firstServiceFolder = (this.databases.FolderDB.ListAllCompleteUserFolders().First())!;
        this.databases.FolderDB.DeleteFolder(firstServiceFolder.LocalId);

        // Create the ledger, and create the pending adds (one delete, one normal)
        var ledger = this.GetLedger();
        var duplicateTitleFolderId = this.databases.FolderDB.CreateFolder(firstServiceFolder.Title).LocalId;
        var normalAddFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check duplicate-titled folder is good
        var syncedFolderWithDuplicateTitle = this.databases.FolderDB.GetFolderByLocalId(duplicateTitleFolderId);
        Assert.NotNull(syncedFolderWithDuplicateTitle);
        Assert.True(syncedFolderWithDuplicateTitle!.ServiceId.HasValue);

        // Check normal folder is good
        var normalSyncedFolder = this.databases.FolderDB.GetFolderByLocalId(normalAddFolderId);
        Assert.NotNull(normalSyncedFolder);
        Assert.True(normalSyncedFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }
    
    [Fact]
    public async Task SyncingPendingDeleteRemovesFolderFromService()
    {
        // Create pending add on empty DB
        var ledger = this.GetLedger();

        // Delete a local folder
        var deletedFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;
        this.databases.FolderDB.DeleteFolder(deletedFolder.LocalId);

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check we can get that same folder, and it now has a service ID
        var nowDeletedFolder = this.databases.MockFolderService.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value);
        Assert.Null(nowDeletedFolder);

        // Check state matches, and the pending changes are gone
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task SyncingPendingDeleteWhenFolderDeletedOnServiceSyncs()
    {
        // Create pending add on empty DB
        var ledger = this.GetLedger();

        // Delete the local folder
        var deletedFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;
        this.databases.FolderDB.DeleteFolder(deletedFolder.LocalId);

        // Delete the same folder on the service
        var serviceFolderToDelete = this.databases.MockFolderService.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value)!;
        this.databases.MockFolderService.FolderDB.DeleteFolder(serviceFolderToDelete.LocalId);

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check we can get that same folder, and it now has a service ID
        var nowDeletedFolder = this.databases.MockFolderService.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value);
        Assert.Null(nowDeletedFolder);

        // Check state matches, and the pending changes are gone
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }
    #endregion

    #region Local & Remote Folder Changes
    [Fact]
    public async Task SyncingPendingAddAndRemoteAddToSyncsAllChanges()
    {
        var ledger = this.GetLedger();

        var newLocalFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var newServiceFolder = this.databases.MockFolderService.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(newLocalFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(newServiceFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task SyncingPendingAddAndRemoteAddAndRemoteDeleteToSyncsAllChanges()
    {
        var ledger = this.GetLedger();

        // Delete a service folder
        var deletedServiceFolder = this.databases.MockFolderService.FolderDB.ListAllCompleteUserFolders().First()!;
        this.databases.MockFolderService.FolderDB.DeleteFolder(deletedServiceFolder.LocalId);

        // Add some folders
        var newLocalFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var newServiceFolder = this.databases.MockFolderService.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(newLocalFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(newServiceFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        // Check that the deleted service folder is missing locally
        Assert.DoesNotContain(deletedServiceFolder, this.databases.FolderDB.ListAllCompleteUserFolders(), new CompareFoldersIgnoringLocalId());

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task SyncingPendingAddAndRemoteAddAndRemoteDeleteAndLocalDeleteToSyncsAllChanges()
    {
        // Start the ledger
        var ledger = this.GetLedger();

        // Create some deletes
        var deletedLocalFolder = this.databases.FolderDB.ListAllCompleteUserFolders().First()!;
        this.databases.FolderDB.DeleteFolder(deletedLocalFolder.LocalId);

        // Make sure the remote delete isn't the one we just deleted locally
        var deletedServiceFolder = this.databases.MockFolderService.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId!.Value != deletedLocalFolder.ServiceId!.Value)!;
        this.databases.MockFolderService.FolderDB.DeleteFolder(deletedServiceFolder.ServiceId!.Value);

        // Create the additions
        var newLocalFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var newServiceFolder = this.databases.MockFolderService.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();
        
        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(newLocalFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(newServiceFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        // Check that the local delete is no longer on the service
        Assert.DoesNotContain(deletedLocalFolder, this.databases.MockFolderService.FolderDB.ListAllCompleteUserFolders(), new CompareFoldersIgnoringLocalId());

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockFolderService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }
    #endregion
}