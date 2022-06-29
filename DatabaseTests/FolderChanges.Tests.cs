using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderChangesTests : IDisposable
{
    private IInstapaperDatabase db;
    private DatabaseFolder CustomLocalFolder1;
    private DatabaseFolder CustomLocalFolder2;

    public FolderChangesTests()
    {
        this.db = TestUtilities.GetDatabase();

        this.CustomLocalFolder1 = this.db.FolderDatabase.CreateFolder("LocalSample1");
        this.CustomLocalFolder2 = this.db.FolderDatabase.CreateFolder("LocalSample2");
    }

    public void Dispose()
    {
        this.db.Dispose();
    }

    [Fact]
    public void CanGetFolderChangesDatabase()
    {
        var changesDb = this.db.FolderChangesDatabase;
        Assert.NotNull(changesDb);
    }

    #region Pending Folder Adds
    [Fact]
    public void CanCreatePendingFolderAdd()
    {
        var change = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Equal(this.CustomLocalFolder1.LocalId, change.FolderLocalId);
        Assert.Equal(this.CustomLocalFolder1.Title, change.Title);
    }

    [Fact]
    public void CreatingPendingFolderAddForNonExistentFolderThrows()
    {
        void Work()
        {
            var change = this.db.FolderChangesDatabase.CreatePendingFolderAdd(99L);
        }

        Assert.Throws<FolderNotFoundException>(Work);
    }

    [Fact]
    public void CanGetPendingFolderAddByLocalFolderId()
    {
        var originalChange = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        var readChange = this.db.FolderChangesDatabase.GetPendingFolderAdd(originalChange.FolderLocalId);
        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void GettingNonExistentPendingFolderAddReturnsNull()
    {
        var change = this.db.FolderChangesDatabase.GetPendingFolderAdd(99L);
        Assert.Null(change);
    }

    [Fact]
    public void CanRemovePendingFolderAddByLocalFolderId()
    {
        var change = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        this.db.FolderChangesDatabase.DeletePendingFolderAdd(change.FolderLocalId);
    }

    [Fact]
    public void RemovingNonExistentPendingFolderAddCompletesWithoutError()
    {
        this.db.FolderChangesDatabase.DeletePendingFolderAdd(1L);
    }

    [Fact]
    public void DeletePendingFolderAddIsActuallyRemoved()
    {
        var change = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        this.db.FolderChangesDatabase.DeletePendingFolderAdd(change.FolderLocalId);

        var result = this.db.FolderChangesDatabase.GetPendingFolderAdd(change.FolderLocalId);
        Assert.Null(result);

        var results = this.db.FolderChangesDatabase.ListPendingFolderAdds();
        Assert.Empty(results);
    }

    [Fact]
    public void CanListAllPendingFolderAdds()
    {
        var changes = this.db.FolderChangesDatabase;
        var change1 = changes.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        var change2 = changes.CreatePendingFolderAdd(this.CustomLocalFolder2.LocalId);

        var allChanges = changes.ListPendingFolderAdds();
        Assert.Equal(2, allChanges.Count);
        Assert.Contains(change1, allChanges);
        Assert.Contains(change2, allChanges);
    }

    [Fact]
    public void ListingPendingFolderAddsWithNoAddsCompletesWithZeroResults()
    {
        var results = this.db.FolderChangesDatabase.ListPendingFolderAdds();
        Assert.Empty(results);
    }

    [Fact]
    public void AddingPendingFolderAddForUnreadFolderShouldFail()
    {
        void Work()
        {
            this.db.FolderChangesDatabase.CreatePendingFolderAdd(WellKnownLocalFolderIds.Unread);
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingPendingFolderAddForArchiveFolderShouldFail()
    {
        void Work()
        {
            this.db.FolderChangesDatabase.CreatePendingFolderAdd(WellKnownLocalFolderIds.Archive);
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingDuplicatePendingFolderAddShouldFail()
    {
        _ = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Throws<DuplicatePendingFolderAddException>(() => this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId));
    }

    [Fact]
    public void DeletingLocalFolderWithPendingAddShouldFail()
    {
        _ = this.db.FolderChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Throws<InvalidOperationException>(() => this.db.FolderDatabase.DeleteFolder(this.CustomLocalFolder1.LocalId));
    }
    #endregion

    #region Pending Folder Deletes
    [Fact]
    public void CanCreatePendingFolderDelete()
    {
        var f = (ServiceId: 1, Title: "Title");
        var change = this.db.FolderChangesDatabase.CreatePendingFolderDelete(f.ServiceId,
                                                                               f.Title);
        Assert.NotEqual(0L, change.ServiceId);
        Assert.Equal(f.ServiceId, change.ServiceId);
        Assert.Equal(f.Title, change.Title);
    }

    [Fact]
    public void CanGetPendingFolderDeleteByServiceId()
    {
        var f = (ServiceId: 1, Title: "Title");
        var originalChange = this.db.FolderChangesDatabase.CreatePendingFolderDelete(f.ServiceId, f.Title);
        var readChange = this.db.FolderChangesDatabase.GetPendingFolderDelete(originalChange.ServiceId);

        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void CanGetPendingFolderDeleteByFolderTitle()
    {
        var f = (ServiceId: 1, Title: "Title");
        var originalChange = this.db.FolderChangesDatabase.CreatePendingFolderDelete(f.ServiceId, f.Title);
        var readChange = this.db.FolderChangesDatabase.GetPendingFolderDeleteByTitle(originalChange.Title);

        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void GettingNonExistentPendingFolderDeleteReturnsNull()
    {
        var change = this.db.FolderChangesDatabase.GetPendingFolderDelete(99L);
        Assert.Null(change);
    }

    [Fact]
    public void CanRemovePendingFolderDeleteByServiceId()
    {
        var change = this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title");
        this.db.FolderChangesDatabase.DeletePendingFolderDelete(change.ServiceId);
    }

    [Fact]
    public void RemovingNonExistentPendingFolderDeleteCompletesWithoutError()
    {
        this.db.FolderChangesDatabase.DeletePendingFolderDelete(1L);
    }

    [Fact]
    public void DeletePendingFolderDeleteIsActuallyRemoved()
    {
        var change = this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title");
        this.db.FolderChangesDatabase.DeletePendingFolderDelete(change.ServiceId);

        var result = this.db.FolderChangesDatabase.GetPendingFolderDelete(change.ServiceId);
        Assert.Null(result);

        var results = this.db.FolderChangesDatabase.ListPendingFolderDeletes();
        Assert.Empty(results);
    }

    [Fact]
    public void CanListAllPendingFolderDeletes()
    {
        var changes = this.db.FolderChangesDatabase;
        var change1 = changes.CreatePendingFolderDelete(1, "Title");
        var change2 = changes.CreatePendingFolderDelete(2, "Title2");

        var allChanges = changes.ListPendingFolderDeletes();
        Assert.Contains(change1, allChanges);
        Assert.Contains(change2, allChanges);
    }

    [Fact]
    public void ListingPendingFolderDeletesWithNoDeletesCompletesWithZeroResults()
    {
        var results = this.db.FolderChangesDatabase.ListPendingFolderDeletes();
        Assert.Empty(results);
    }

    [Fact]
    public void AddingPendingFolderDeleteForUnreadFolderShouldFail()
    {
        void Work()
        {
            this.db.FolderChangesDatabase.CreatePendingFolderDelete(WellKnownServiceFolderIds.Unread, "Unread");
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingPendingFolderDeleteForArchiveFolderShouldFail()
    {
        void Work()
        {
            this.db.FolderChangesDatabase.CreatePendingFolderDelete(WellKnownServiceFolderIds.Archive, "Archive");
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForServiceId()
    {
        _ = this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title2"));
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForTitle()
    {
        _ = this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.FolderChangesDatabase.CreatePendingFolderDelete(98L, "Title"));
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForServiceIdAndTitle()
    {
        _ = this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.FolderChangesDatabase.CreatePendingFolderDelete(99L, "Title"));
    }
    #endregion
}