using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class LedgerTests : IAsyncLifetime
{
    private IInstapaperDatabase? db;
    private IFolderDatabase? folders;
    private IFolderChangesDatabase? folderChanges;

    private Ledger? ledger;

    public async Task InitializeAsync()
    {
        this.db = await TestUtilities.GetDatabase();
        this.folders = this.db.FolderDatabase;
        this.folderChanges = this.db.FolderChangesDatabase;
        this.ledger = new(this.folders, this.folderChanges, this.db.ArticleChangesDatabase);
    }

    public Task DisposeAsync()
    {
        this.ledger?.Dispose();
        this.db?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void AddsPendingAddWhenFolderIsCreated()
    {
        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());

        var folder = this.folders!.CreateFolder("Sample");
        var pendingAdds = this.folderChanges.ListPendingFolderAdds();
        Assert.Equal(1, pendingAdds.Count);
        Assert.Equal(folder.Title, pendingAdds[0].Title);
        Assert.Equal(folder.LocalId, pendingAdds[0].FolderLocalId);
    }

    [Fact]
    public void DoesNotAddPendingAddWhenKnownFolderIsAdded()
    {
        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());

        var folder = this.folders!.AddKnownFolder("Known Folder", 1L, 1L, true);
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
    }

    [Fact]
    public void DeletingKnownFolderCreatesPendingDeleteWithServiceProperties()
    {
        var folder = this.folders!.AddKnownFolder("Sample", 1L, 1L, true);
        
        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
        this.folders!.DeleteFolder(folder.LocalId);

        var pendingDeletes = this.folderChanges!.ListPendingFolderDeletes();
        Assert.Equal(1, pendingDeletes.Count);
        Assert.Equal(folder.ServiceId, pendingDeletes[0].ServiceId);
        Assert.Equal(folder.Title, pendingDeletes[0].Title);
    }

    [Fact]
    public void DeletingAFolderTwiceDoesNotCreateDuplicatePendingEntries()
    {
        var folder = this.folders!.AddKnownFolder("Known Folder", 1L, 1L, true);

        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
        this.folders!.DeleteFolder(folder.LocalId);
        this.folders!.DeleteFolder(folder.LocalId);
        Assert.Equal(1, this.folderChanges!.ListPendingFolderDeletes().Count);
    }

    [Fact]
    public void PendingAddsAreCleanedUpWhenAFolderWithAPendingAddIsDeletedAndDoesntCreateANewDelete()
    {
        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());
        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
        var folder = this.folders!.CreateFolder("Sample");
        Assert.NotEmpty(this.folderChanges.ListPendingFolderAdds());

        this.folders!.DeleteFolder(folder.LocalId);

        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());
        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
    }

    [Fact]
    public void AddingPreviouslyDeletedKnownFolderResurrectsItsServiceProperties()
    {
        var folder = this.folders!.AddKnownFolder("Sample", 1L, 1L, true);
        
        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
        this.folders!.DeleteFolder(folder.LocalId);

        var resurrectedFolder = this.folders.CreateFolder(folder.Title);
        Assert.Equal(folder.ServiceId, resurrectedFolder.ServiceId);

        Assert.Empty(this.folderChanges!.ListPendingFolderDeletes());
        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());
    }

    [Fact]
    public void DeletingFolderThatHasPendingArticleMoveToItThrowsException()
    {
        // Move to init
        var sampleArticle = this.db!.ArticleDatabase.AddArticleToFolder(TestUtilities.GetRandomArticle(), WellKnownLocalFolderIds.Unread);
        var destinationFolder = this.folders!.AddKnownFolder("Sample", 1L, 1L, true);

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticle.Id, destinationFolder.LocalId);

        Assert.Throws<FolderHasPendingArticleMoveException>(() => this.db!.FolderDatabase.DeleteFolder(destinationFolder.LocalId));
    }

    [Fact]
    public void LedgerDoesNotOperateAfterBeingDisposed()
    {
        this.ledger!.Dispose();
        this.ledger = null;

        var folder = this.folders!.CreateFolder("Something");
        Assert.Empty(this.folderChanges!.ListPendingFolderAdds());

        var knownFolder = this.folders!.AddKnownFolder("Known", 1L, 1L, true);
        this.folders!.DeleteFolder(knownFolder.LocalId);
    }
}