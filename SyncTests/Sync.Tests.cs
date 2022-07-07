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
        IDbConnection ServiceConnection,
        FoldersClientOverDatabase MockService
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
        var (connection, folderDb, folderChangesDb) = TestUtilities.GetEmptyDatabase();
        this.databases = (connection, folderDb, folderChangesDb, this.databases.ServiceConnection, this.databases.MockService);
        this.SetSyncEngineFromDatabases();

        // Make sure we have an empty database for this test.
        Assert.Equal(DEFAULT_FOLDER_COUNT, this.databases.FolderDB.ListAllFolders().Count);
    }

    private void DisposeLocalDatabase()
    {
        this.databases.Connection.Close();
        this.databases.Connection.Dispose();
    }

    public void Dispose()
    {
        this.DisposeLocalDatabase();
        this.databases.ServiceConnection.Close();
        this.databases.ServiceConnection.Dispose();
    }

    [Fact]
    public async Task SyncOnEmptyDatabaseCreatesCorrectFolders()
    {
        this.SwitchToEmptyLocalDatabase();
        
        // Perform the sync, which should pull down remote folders
        await this.syncEngine.SyncFolders();

        // Check that the folders match
        var remoteFolders = this.databases.MockService.FolderDB.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        var localFolders = this.databases.FolderDB.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        Assert.True(localFolders.Count() > 0);
        Assert.Equal(remoteFolders, localFolders);
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
}