using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class SyncTests : IDisposable
{
    private const int DEFAULT_FOLDER_COUNT = 2;

    private (
        IDbConnection Connection,
        IFolderDatabase FolderDB,
        IFolderChangesDatabase FolderChangesDB,
        IArticleDatabase ArticleDB,
        IDbConnection ServiceConnection,
        MockFolderService MockService
    ) databases;
    private Sync syncEngine;

    public SyncTests()
    {
        this.databases = TestUtilities.GetDatabases();
        this.SetSyncEngineFromDatabases();
    }

    [MemberNotNull(nameof(syncEngine))]
    private void SetSyncEngineFromDatabases()
    {
        this.syncEngine = new Sync(this.databases.FolderDB,
                            this.databases.FolderChangesDB,
                            this.databases.MockService);
    }

    private void SwitchToEmptyLocalDatabase()
    {
        this.DisposeLocalDatabase();
        var (connection, folderDb, folderChangesDb, articlDb) = TestUtilities.GetEmptyDatabase();
        this.databases = (connection, folderDb, folderChangesDb, articlDb, this.databases.ServiceConnection, this.databases.MockService);
        this.SetSyncEngineFromDatabases();

        // Make sure we have an empty database for this test.
        Assert.Equal(DEFAULT_FOLDER_COUNT, this.databases.FolderDB.ListAllFolders().Count);
    }

    private void SwitchToEmptyServiceDatabase()
    {
        this.DisposeServiceDatabase();

        var (connection, folderDb, _, _) = TestUtilities.GetEmptyDatabase();
        this.databases = (
            this.databases.Connection,
            this.databases.FolderDB,
            this.databases.FolderChangesDB,
            this.databases.ArticleDB,
            connection,
            new MockFolderService(folderDb)
        );

        Assert.NotEqual(this.databases.FolderDB.ListAllCompleteUserFolders().Count, folderDb.ListAllCompleteUserFolders().Count);

        this.SetSyncEngineFromDatabases();
    }

    private void DisposeLocalDatabase()
    {
        this.databases.Connection.Close();
        this.databases.Connection.Dispose();
    }

    private void DisposeServiceDatabase()
    {
        this.databases.ServiceConnection.Close();
        this.databases.ServiceConnection.Dispose();
    }

    public void Dispose()
    {
        this.DisposeLocalDatabase();
        this.DisposeServiceDatabase();
    }

    [Fact]
    public async Task SyncingEmptyDatabasesCreatesEmptyState()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
    }

    #region Service-only changes sync
    [Fact]
    public async Task SyncOnEmptyDatabaseCreatesCorrectFolders()
    {
        this.SwitchToEmptyLocalDatabase();
        
        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.True(this.databases.FolderDB.ListAllCompleteUserFolders().Count > 0);
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
    }

    [Fact]
    public async Task FoldersTitleDifferentOnServiceAreUpdatedDuringSync()
    {
        // Pick a folder to update
        var firstLocalUserFolder = (this.databases.FolderDB.ListAllCompleteUserFolders().First())!;

        // Update the title on the *service*
        var remoteFolder = (this.databases.MockService.FolderDB.GetFolderByServiceId(firstLocalUserFolder.ServiceId!.Value))!;
        remoteFolder = this.databases.MockService.FolderDB.UpdateFolder(
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
        var remoteToDelete = (this.databases.MockService.FolderDB.ListAllCompleteUserFolders().First())!;
        this.databases.MockService.FolderDB.DeleteFolder(remoteToDelete.LocalId);

        await this.syncEngine.SyncFolders();

        var localFolders = this.databases.FolderDB.ListAllCompleteUserFolders();
        Assert.Equal(targetFolderCount, localFolders.Count);
        Assert.DoesNotContain(remoteToDelete, localFolders, new CompareFoldersIgnoringLocalId());
    }

    [Fact]
    public async Task FoldersAddedOnServiceAreAddedWhenLocalDatabaseIsntEmpty()
    {
        var remoteFolder = this.databases.MockService.FolderDB.AddCompleteFolderToDb();
        var localFolderCount = this.databases.FolderDB.ListAllCompleteUserFolders().Count;

        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        Assert.Equal(localFolderCount + 1, this.databases.FolderDB.ListAllCompleteUserFolders().Count());
        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
    }

    [Fact]
    public async Task AddedAndRemovedFoldersOnServiceAreCorrectlySynced()
    {
        var deletedRemoteFolder = (this.databases.MockService.FolderDB.ListAllCompleteUserFolders().First())!;
        var addedRemoteFolder = this.databases.MockService.FolderDB.AddCompleteFolderToDb();

        await this.syncEngine.SyncFolders();

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
    }
    #endregion

    #region Local-Only changes sync
    [Fact]
    public async Task SyncingPendingAddToEmptyServiceAddsRemoteFolder()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        var ledger = InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
        var newFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task MultiplePendingAddsToEmptyServiceAddsAllPendingEdits()
    {
        this.SwitchToEmptyServiceDatabase();
        this.SwitchToEmptyLocalDatabase();

        var ledger = InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
        var firstNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var secondNewFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder 2").LocalId;

        await this.syncEngine.SyncFolders();

        var firstSyncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(firstNewFolderId);
        Assert.NotNull(firstSyncedNewFolder);
        Assert.True(firstSyncedNewFolder!.ServiceId.HasValue);

        var secondSyncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(firstNewFolderId);
        Assert.NotNull(secondSyncedNewFolder);
        Assert.True(secondSyncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task PendingAddWithDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        this.SwitchToEmptyLocalDatabase();

        var ledger = InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
        var firstRemoteFolderTitle = (this.databases.MockService.FolderDB.ListAllCompleteUserFolders().First()!).Title;
        var newFolderId = this.databases.FolderDB.CreateFolder(firstRemoteFolderTitle).LocalId;

        await this.syncEngine.SyncFolders();

        var syncedNewFolder = this.databases.FolderDB.GetFolderByLocalId(newFolderId);
        Assert.NotNull(syncedNewFolder);
        Assert.True(syncedNewFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }

    [Fact]
    public async Task MultipleAddsWithWithOneDuplicateTitleSuccessfullySyncsAndUpdatesLocalData()
    {
        this.SwitchToEmptyLocalDatabase();

        var ledger = InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
        var firstRemoteFolderTitle = (this.databases.MockService.FolderDB.ListAllCompleteUserFolders().First()!).Title;
        var duplicateTitleFolderId = this.databases.FolderDB.CreateFolder(firstRemoteFolderTitle).LocalId;
        var normalAddFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;

        await this.syncEngine.SyncFolders();

        var syncedFolderWithDuplicateTitle = this.databases.FolderDB.GetFolderByLocalId(duplicateTitleFolderId);
        Assert.NotNull(syncedFolderWithDuplicateTitle);
        Assert.True(syncedFolderWithDuplicateTitle!.ServiceId.HasValue);

        var normalSyncedFolder = this.databases.FolderDB.GetFolderByLocalId(normalAddFolderId);
        Assert.NotNull(normalSyncedFolder);
        Assert.True(normalSyncedFolder!.ServiceId.HasValue);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }
    #endregion

    #region Local & Remote Folder Changes
    [Fact]
    public async Task SyncingPendingAddAndRemoteAddToSyncsAllChanges()
    {
        var ledger = InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);

        var newLocalFolderId = this.databases.FolderDB.CreateFolder("Local Only Folder").LocalId;
        var remoteFolder = this.databases.MockService.FolderDB.AddCompleteFolderToDb();

        await this.syncEngine.SyncFolders();

        // Check the local Pending add round tripped
        var syncedLocalFolder = this.databases.FolderDB.GetFolderByLocalId(newLocalFolderId);
        Assert.NotNull(syncedLocalFolder);
        Assert.True(syncedLocalFolder!.ServiceId.HasValue);

        // Check that the remote add is now available locally
        var remoteFolderAvailableLocally = this.databases.FolderDB.GetFolderByServiceId(remoteFolder.ServiceId!.Value);
        Assert.NotNull(remoteFolderAvailableLocally);

        TestUtilities.AssertFoldersListsAreSame(this.databases.FolderDB, this.databases.MockService.FolderDB);
        this.databases.FolderChangesDB.AssertNoPendingAdds();
    }
    #endregion
}