namespace Codevoid.Test.Storyvoid;

public sealed class FolderSyncTests : BaseSyncTest
{
    [Fact]
    public async Task SyncingEmptyDatabasesCreatesEmptyState()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
    }

    #region Remote-only Folder changes sync
    [Fact]
    public async Task SyncOnEmptyDatabaseCreatesCorrectFolders()
    {
        this.SwitchToEmptyLocalDatabase();
        
        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.True(this.databases.FolderDB.ListAllCompleteUserFolders().Count() > 0);
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
    }

    [Fact]
    public async Task FoldersTitleDifferentOnRemoteAreUpdatedDuringSync()
    {
        // Pick a folder to update
        var firstLocalUserFolder = this.databases.FolderDB.FirstCompleteUserFolder();

        // Update the title on the *service*
        var remoteFolder = (this.service.FoldersClient.FolderDB.GetFolderByServiceId(firstLocalUserFolder.ServiceId!.Value))!;
        remoteFolder = this.service.FoldersClient.FolderDB.UpdateFolder(
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
        var targetFolderCount = this.databases.FolderDB.ListAllCompleteUserFolders().Count() - 1;

        // Create a folder that only exists remotely
        var remoteToDelete = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        this.service.FoldersClient.FolderDB.DeleteFolder(remoteToDelete.LocalId);

        await this.syncEngine.SyncFolders();

        var localFolders = this.databases.FolderDB.ListAllCompleteUserFolders();
        Assert.Equal(targetFolderCount, localFolders.Count());
        Assert.DoesNotContain(remoteToDelete, localFolders, new CompareFoldersIgnoringLocalId());
    }

    [Fact]
    public async Task FoldersAddedRemotelyAreAddedWhenLocalDatabaseIsntEmpty()
    {
        var remoteFolder = this.service.FoldersClient.FolderDB.AddCompleteFolderToDb();
        var localFolderCount = this.databases.FolderDB.ListAllCompleteUserFolders().Count();

        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.Equal(localFolderCount + 1, this.databases.FolderDB.ListAllCompleteUserFolders().Count());
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
    }

    [Fact]
    public async Task AddedAndRemovedFoldersOnRemotelyAreCorrectlySynced()
    {
        var deletedRemoteFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        var addedRemoteFolder = this.service.FoldersClient.FolderDB.AddCompleteFolderToDb();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
    }
    
    [Fact]
    public async Task FoldersThatAreSetNotToSyncAreRemovedDuringSync()
    {
        var remoteFolderSetToNotSync = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        remoteFolderSetToNotSync = this.service.FoldersClient.FolderDB.UpdateFolder(
            localId: remoteFolderSetToNotSync.LocalId,
            serviceId: remoteFolderSetToNotSync.ServiceId,
            title: remoteFolderSetToNotSync.Title,
            position: remoteFolderSetToNotSync.Position,
            shouldSync: false
        );

        await this.syncEngine.SyncFolders();

        var localFolders = this.databases.FolderDB.ListAllCompleteUserFolders();
        Assert.DoesNotContain(remoteFolderSetToNotSync, localFolders, new CompareFoldersIgnoringLocalId());
    }
    #endregion

    #region Local-Only Folder changes sync
    [Fact]
    public async Task SyncingPendingAddToEmptyRemoteAddsRemoteFolder()
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
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task SyncingPendingAddToExistingRemoteAddsRemoteFolder()
    {
        var ledger = this.GetLedger();
        var newFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task MultiplePendingAddsToEmptyRemoteAddsAllPendingEdits()
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

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task PendingAddWithDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        // Remove a folder from the service, but save it's title
        var firstSeviceFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderDB.DeleteFolder(firstSeviceFolder.LocalId);

        // Start the ledger, and create a pending edit
        var ledger = this.GetLedger();
        var newFolderId = this.databases.FolderDB.CreateFolder(firstSeviceFolder.Title).LocalId;
        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task MultipleAddsWithWithOneDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        var remoteFirstFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderDB.DeleteFolder(remoteFirstFolder.LocalId);

        // Create the ledger, and create the pending adds (one delete, one normal)
        var ledger = this.GetLedger();
        var duplicateTitleFolderId = this.databases.FolderDB.CreateFolder(remoteFirstFolder.Title).LocalId;
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

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }
    
    [Fact]
    public async Task SyncingPendingDeleteRemovesFolderFromRemote()
    {
        // Create pending add on empty DB
        var ledger = this.GetLedger();

        // Delete a local folder
        var deletedFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderDB.DeleteFolder(deletedFolder.LocalId);

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check we can get that same folder, and it now has a service ID
        var nowDeletedFolder = this.service.FoldersClient.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value);
        Assert.Null(nowDeletedFolder);

        // Check state matches, and the pending changes are gone
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task SyncingPendingDeleteWhenFolderDeletedOnServiceSyncs()
    {
        // Create pending add on empty DB
        var ledger = this.GetLedger();

        // Delete the local folder
        var deletedFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderDB.DeleteFolder(deletedFolder.LocalId);

        // Delete the same folder remotely
        var remoteFolderToDelete = this.service.FoldersClient.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value)!;
        this.service.FoldersClient.FolderDB.DeleteFolder(remoteFolderToDelete.LocalId);

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check we can get that same folder, and it now has a service ID
        var nowDeletedFolder = this.service.FoldersClient.FolderDB.GetFolderByServiceId(deletedFolder.ServiceId!.Value);
        Assert.Null(nowDeletedFolder);

        // Check state matches, and the pending changes are gone
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }
    #endregion

    #region Local & Remote Folder Changes
    [Fact]
    public async Task SyncingPendingAddAndRemoteAddToSyncsAllChanges()
    {
        var ledger = this.GetLedger();

        var localNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var remoteNewFolder = this.service.FoldersClient.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(localNewFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(remoteNewFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task SyncingPendingAddAndRemoteAddAndRemoteDeleteToSyncsAllChanges()
    {
        var ledger = this.GetLedger();

        // Delete a remote folder
        var remoteDeletedFolder = this.service.FoldersClient.FolderDB.FirstCompleteUserFolder();
        this.service.FoldersClient.FolderDB.DeleteFolder(remoteDeletedFolder.LocalId);

        // Add some folders
        var localNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var remoteNewFolder = this.service.FoldersClient.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();

        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(localNewFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(remoteNewFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        // Check that the deleted service folder is missing locally
        Assert.DoesNotContain(remoteDeletedFolder, this.databases.FolderDB.ListAllCompleteUserFolders(), new CompareFoldersIgnoringLocalId());

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }

    [Fact]
    public async Task SyncingPendingAddAndRemoteAddAndRemoteDeleteAndLocalDeleteToSyncsAllChanges()
    {
        // Start the ledger
        var ledger = this.GetLedger();

        // Create some deletes
        var deletedLocalFolder = this.databases.FolderDB.FirstCompleteUserFolder();
        this.databases.FolderDB.DeleteFolder(deletedLocalFolder.LocalId);

        // Make sure the remote delete isn't the one we just deleted locally
        var remoteDeletedFolder = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId!.Value != deletedLocalFolder.ServiceId!.Value)!;
        this.service.FoldersClient.FolderDB.DeleteFolder(remoteDeletedFolder.ServiceId!.Value);

        // Create the additions
        var localNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var localNewFolder = this.service.FoldersClient.FolderDB.AddCompleteFolderToDb();

        ledger.Dispose();
        
        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(localNewFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(localNewFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        // Check that the local delete is no longer available remotely
        Assert.DoesNotContain(deletedLocalFolder, this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders(), new CompareFoldersIgnoringLocalId());

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.service.FoldersClient.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingEdits();
    }
    #endregion
}