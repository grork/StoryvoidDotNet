using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderChangesTests : IDisposable
{
    private IDbConnection connection;
    private IFolderChangesDatabase db;
    private DatabaseFolder CustomLocalFolder1;
    private DatabaseFolder CustomLocalFolder2;

    public FolderChangesTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.db = new FolderChanges(this.connection);
        var folderDb = new FolderDatabase(this.connection);

        this.CustomLocalFolder1 = folderDb.CreateFolder("LocalSample1");
        this.CustomLocalFolder2 = folderDb.CreateFolder("LocalSample2");
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose(); 
    }

    [Fact]
    public void CanGetFolderChangesDatabase()
    {
        Assert.NotNull(this.db);
    }

    #region Pending Folder Adds
    [Fact]
    public void CanCreatePendingFolderAdd()
    {
        var change = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Equal(this.CustomLocalFolder1.LocalId, change.FolderLocalId);
        Assert.Equal(this.CustomLocalFolder1.Title, change.Title);
    }

    [Fact]
    public void CreatingPendingFolderAddForNonExistentFolderThrows()
    {
        Assert.Throws<FolderNotFoundException>(() => this.db.CreatePendingFolderAdd(99L));
    }

    [Fact]
    public void CanGetPendingFolderAddByLocalFolderId()
    {
        var originalChange = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        var readChange = this.db.GetPendingFolderAdd(originalChange.FolderLocalId);
        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void GettingNonExistentPendingFolderAddReturnsNull()
    {
        var change = this.db.GetPendingFolderAdd(99L);
        Assert.Null(change);
    }

    [Fact]
    public void CanRemovePendingFolderAddByLocalFolderId()
    {
        var change = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        this.db.DeletePendingFolderAdd(change.FolderLocalId);
    }

    [Fact]
    public void RemovingNonExistentPendingFolderAddCompletesWithoutError()
    {
        this.db.DeletePendingFolderAdd(1L);
    }

    [Fact]
    public void DeletePendingFolderAddIsActuallyRemoved()
    {
        var change = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        this.db.DeletePendingFolderAdd(change.FolderLocalId);

        var result = this.db.GetPendingFolderAdd(change.FolderLocalId);
        Assert.Null(result);

        var results = this.db.ListPendingFolderAdds();
        Assert.Empty(results);
    }

    [Fact]
    public void CanListAllPendingFolderAdds()
    {
        var change1 = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        var change2 = this.db.CreatePendingFolderAdd(this.CustomLocalFolder2.LocalId);

        var allChanges = this.db.ListPendingFolderAdds();
        Assert.Equal(2, allChanges.Count);
        Assert.Contains(change1, allChanges);
        Assert.Contains(change2, allChanges);
    }

    [Fact]
    public void ListingPendingFolderAddsWithNoAddsCompletesWithZeroResults()
    {
        var results = this.db.ListPendingFolderAdds();
        Assert.Empty(results);
    }

    [Fact]
    public void AddingPendingFolderAddForUnreadFolderShouldFail()
    {
        Assert.Throws<InvalidOperationException>(() => this.db.CreatePendingFolderAdd(WellKnownLocalFolderIds.Unread));
    }

    [Fact]
    public void AddingPendingFolderAddForArchiveFolderShouldFail()
    {
        Assert.Throws<InvalidOperationException>(() => this.db.CreatePendingFolderAdd(WellKnownLocalFolderIds.Archive));
    }

    [Fact]
    public void AddingDuplicatePendingFolderAddShouldFail()
    {
        _ = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Throws<DuplicatePendingFolderAddException>(() => this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId));
    }

    [Fact]
    public void DeletingLocalFolderWithPendingAddShouldFail()
    {
        _ = this.db.CreatePendingFolderAdd(this.CustomLocalFolder1.LocalId);
        Assert.Throws<InvalidOperationException>(() => new FolderDatabase(this.connection).DeleteFolder(this.CustomLocalFolder1.LocalId));
    }
    #endregion

    #region Pending Folder Deletes
    [Fact]
    public void CanCreatePendingFolderDelete()
    {
        var f = (ServiceId: 1, Title: "Title");
        var change = this.db.CreatePendingFolderDelete(f.ServiceId,
                                                                               f.Title);
        Assert.NotEqual(0L, change.ServiceId);
        Assert.Equal(f.ServiceId, change.ServiceId);
        Assert.Equal(f.Title, change.Title);
    }

    [Fact]
    public void CanGetPendingFolderDeleteByServiceId()
    {
        var f = (ServiceId: 1, Title: "Title");
        var originalChange = this.db.CreatePendingFolderDelete(f.ServiceId, f.Title);
        var readChange = this.db.GetPendingFolderDelete(originalChange.ServiceId);

        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void CanGetPendingFolderDeleteByFolderTitle()
    {
        var f = (ServiceId: 1, Title: "Title");
        var originalChange = this.db.CreatePendingFolderDelete(f.ServiceId, f.Title);
        var readChange = this.db.GetPendingFolderDeleteByTitle(originalChange.Title);

        Assert.Equal(originalChange, readChange);
    }

    [Fact]
    public void GettingNonExistentPendingFolderDeleteReturnsNull()
    {
        var change = this.db.GetPendingFolderDelete(99L);
        Assert.Null(change);
    }

    [Fact]
    public void CanRemovePendingFolderDeleteByServiceId()
    {
        var change = this.db.CreatePendingFolderDelete(99L, "Title");
        this.db.DeletePendingFolderDelete(change.ServiceId);
    }

    [Fact]
    public void RemovingNonExistentPendingFolderDeleteCompletesWithoutError()
    {
        this.db.DeletePendingFolderDelete(1L);
    }

    [Fact]
    public void DeletePendingFolderDeleteIsActuallyRemoved()
    {
        var change = this.db.CreatePendingFolderDelete(99L, "Title");
        this.db.DeletePendingFolderDelete(change.ServiceId);

        var result = this.db.GetPendingFolderDelete(change.ServiceId);
        Assert.Null(result);

        var results = this.db.ListPendingFolderDeletes();
        Assert.Empty(results);
    }

    [Fact]
    public void CanListAllPendingFolderDeletes()
    {
        var change1 = this.db.CreatePendingFolderDelete(1, "Title");
        var change2 = this.db.CreatePendingFolderDelete(2, "Title2");

        var allChanges = this.db.ListPendingFolderDeletes();
        Assert.Contains(change1, allChanges);
        Assert.Contains(change2, allChanges);
    }

    [Fact]
    public void ListingPendingFolderDeletesWithNoDeletesCompletesWithZeroResults()
    {
        var results = this.db.ListPendingFolderDeletes();
        Assert.Empty(results);
    }

    [Fact]
    public void AddingPendingFolderDeleteForUnreadFolderShouldFail()
    {
        void Work()
        {
            this.db.CreatePendingFolderDelete(WellKnownServiceFolderIds.Unread, "Unread");
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingPendingFolderDeleteForArchiveFolderShouldFail()
    {
        void Work()
        {
            this.db.CreatePendingFolderDelete(WellKnownServiceFolderIds.Archive, "Archive");
        }

        Assert.Throws<InvalidOperationException>(Work);
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForServiceId()
    {
        _ = this.db.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.CreatePendingFolderDelete(99L, "Title2"));
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForTitle()
    {
        _ = this.db.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.CreatePendingFolderDelete(98L, "Title"));
    }

    [Fact]
    public void AddingDuplicatePendingFolderDeleteShouldForServiceIdAndTitle()
    {
        _ = this.db.CreatePendingFolderDelete(99L, "Title");
        Assert.Throws<DuplicatePendingFolderDeleteException>(() => this.db.CreatePendingFolderDelete(99L, "Title"));
    }
    #endregion
}