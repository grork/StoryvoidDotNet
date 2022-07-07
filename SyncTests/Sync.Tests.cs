using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class SyncTests : IDisposable
{
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
        this.syncEngine = new Sync(this.databases.FolderDB,
                                   this.databases.FolderChangesDB,
                                   this.databases.MockService);
    }

    public void Dispose()
    {
        this.databases.Connection.Close();
        this.databases.Connection.Dispose();
        this.databases.ServiceConnection.Close();
        this.databases.ServiceConnection.Dispose();
    }

    [Fact]
    public async Task FoldersThatAreNotLocalAreAddedToTheLocalDatabase()
    {
        Assert.Equal(2, this.databases.FolderDB.ListAllFolders().Count);

        // Create a folder that only exists remotely
        var mockRemoteFolder = this.databases.MockService.FolderDB.AddKnownFolder(
            title: "Something New",
            serviceId: 999L,
            position: 999L,
            shouldSync: true
        );

        await this.syncEngine.SyncFolders();

        Assert.Equal(3, this.databases.FolderDB.ListAllFolders().Count);
        var folderThatWasSynced = this.databases.FolderDB.GetFolderByServiceId(mockRemoteFolder.ServiceId!.Value);
        Assert.NotNull(folderThatWasSynced);
        Assert.Equal(mockRemoteFolder, folderThatWasSynced);
    }

    [Fact]
    public async Task FoldersTitleDifferentOnServiceIsUpdatedLocally()
    {
        Assert.Equal(2, this.databases.FolderDB.ListAllFolders().Count);

        // Create a folder that only exists remotely
        var mockRemoteFolder = this.databases.MockService.FolderDB.AddKnownFolder(
            title: "Something New",
            serviceId: 999L,
            position: 999L,
            shouldSync: true
        );

        var localFolderWithDifferentTitle = this.databases.FolderDB.AddKnownFolder(
            title: "Something Old",
            serviceId: 999L,
            position: 999L,
            shouldSync: true
        );

        await this.syncEngine.SyncFolders();

        var folderThatWasSynced = this.databases.FolderDB.GetFolderByServiceId(mockRemoteFolder.ServiceId!.Value);
        Assert.NotNull(folderThatWasSynced);
        Assert.Equal(mockRemoteFolder, folderThatWasSynced);
    }

    [Fact]
    public async Task FoldersDeletedOnServiceAreRemovedFromLocalDatabase()
    {
        // Create a folder that only exists remotely
        _ = this.databases.FolderDB.AddKnownFolder(
            title: "Something New",
            serviceId: 999L,
            position: 999L,
            shouldSync: true
        );

        await this.syncEngine.SyncFolders();

        Assert.Equal(2, this.databases.FolderDB.ListAllFolders().Count);
    }
}