using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class SyncTests : IDisposable
{
    private (
        IDbConnection Connection,
        IFolderDatabase FolderDB,
        IFolderChangesDatabase FolderChangesDB
    ) databases;
    private Sync syncEngine;

    public SyncTests()
    {
        this.databases = TestUtilities.GetDatabases();
        this.syncEngine = new Sync(this.databases.FolderDB,
                                   this.databases.FolderChangesDB,
                                   new MockFolders());
    }

    public void Dispose()
    {
        this.databases.Connection.Close();
        this.databases.Connection.Dispose();
    }

    [Fact]
    public void CanInstantiate()
    {
        Assert.NotNull(this.syncEngine);
    }

    [Fact]
    public void CanGetDefaultFolders()
    {
        Assert.Equal(2, this.syncEngine.FolderCount);
    }
}