using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderTransactionTests : IAsyncLifetime
{
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
    public void ExceptionDuringFolderCreationAddEventRollsBackEntireChange()
    {
        this.db!.FolderAdded += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderAdd(folder.LocalId);
            throw new Exception("Test Exception");
        };

        Assert.Throws<Exception>(() => this.db!.CreateFolder("Sample"));

        Assert.Equal(2, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringAddingKnownFolderAddEventRollsBackEntireChange()
    {
        this.db!.FolderAdded += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderAdd(folder.LocalId);
            throw new Exception("Test Exception");
        };

        Assert.Throws<Exception>(() => this.db!.AddKnownFolder("Sample", 1L, 1L, true));

        Assert.Equal(2, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringWillDeleteFolderEventRollsBackEntireChange()
    {
        var createdFolder = this.db!.AddKnownFolder("Sample", 10L, 1L, true);
        this.db!.FolderWillBeDeleted += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderDelete((createdFolder!).ServiceId!.Value, createdFolder.Title);
            throw new Exception("Sample Exception");
        };
        Assert.Throws<Exception>(() => this.db!.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(3, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderDeletes());
    }
}