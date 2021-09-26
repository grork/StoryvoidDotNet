using System;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class ChangesDatabaseTests : IAsyncLifetime
    {
        private IInstapaperDatabase? db;
        private DatabaseFolder? CustomLocalFolder1;
        private DatabaseFolder? CustomLocalFolder2;

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();

            this.CustomLocalFolder1 = this.db.FolderDatabase.CreateFolder("LocalSample1");
            this.CustomLocalFolder2 = this.db.FolderDatabase.CreateFolder("LocalSample2");
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void CanGetChangesDatabase()
        {
            var changesDb = this.db!.ChangesDatabase;
            Assert.NotNull(changesDb);
        }

        #region Pending Folder Adds
        [Fact]
        public void CanCreatePendingFolderAdd()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            Assert.NotEqual(0L, change.ChangeId);
            Assert.Equal(this.CustomLocalFolder1!.LocalId, change.FolderLocalId);
            Assert.Equal(this.CustomLocalFolder1!.Title, change.Title);
        }

        [Fact]
        public void CreatingPendingFolderAddForNonExistentFolderThrows()
        {
            void Work()
            {
                var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(99L);
            }

            Assert.Throws<FolderNotFoundException>(Work);
        }

        [Fact]
        public void CanGetPendingFolderAddByChangeId()
        {
            var originalChange = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var readChange = this.db!.ChangesDatabase.GetPendingFolderAdd(originalChange.ChangeId);
            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public void CanGetPendingFolderAddByLocalFolderId()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var changeViaFolderId = this.db!.ChangesDatabase.GetPendingFolderAddByLocalFolderId(this.CustomLocalFolder1!.LocalId);
            Assert.Equal(change, changeViaFolderId);
        }

        [Fact]
        public void GettingNonExistentPendingFolderAddReturnsNull()
        {
            var change = this.db!.ChangesDatabase.GetPendingFolderAdd(99L);
            Assert.Null(change);
        }

        [Fact]
        public void CanRemovePendingFolderAddByChangeId()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            this.db!.ChangesDatabase.RemovePendingFolderAdd(change.ChangeId);
        }

        [Fact]
        public void RemovingNonExistentPendingFolderAddCompletesWithoutError()
        {
            this.db!.ChangesDatabase.RemovePendingFolderAdd(1L);
        }

        [Fact]
        public void RemovedPendingFolderAddIsActuallyRemoved()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            this.db!.ChangesDatabase.RemovePendingFolderAdd(change.ChangeId);

            var result = this.db!.ChangesDatabase.GetPendingFolderAdd(change.ChangeId);
            Assert.Null(result);

            var results = this.db!.ChangesDatabase.ListPendingFolderAdds();
            Assert.Empty(results);
        }

        [Fact]
        public void CanListAllPendingFolderAdds()
        {
            var changes = this.db!.ChangesDatabase;
            var change1 = changes.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var change2 = changes.CreatePendingFolderAdd(this.CustomLocalFolder2!.LocalId);

            var allChanges = changes.ListPendingFolderAdds();
            Assert.Equal(2, allChanges.Count);
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public void ListingPendingFolderAddsWithNoAddsCompletesWithZeroResults()
        {
            var results = this.db!.ChangesDatabase.ListPendingFolderAdds();
            Assert.Empty(results);
        }

        [Fact]
        public void AddingPendingFolderAddForUnreadFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderAdd(WellKnownLocalFolderIds.Unread);
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingPendingFolderAddForArchiveFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderAdd(WellKnownLocalFolderIds.Archive);
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingDuplicatePendingFolderAddShouldFail()
        {
            _ = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            Assert.Throws<DuplicatePendingFolderAdd>(() => this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId));
        }

        [Fact]
        public void DeletingLocalFolderWithPendingAddShouldFail()
        {
            _ = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            Assert.Throws<InvalidOperationException>(() => this.db!.FolderDatabase.DeleteFolder(this.CustomLocalFolder1!.LocalId));
        }
        #endregion

        #region Pending Folder Deletes
        [Fact]
        public void CanCreatePendingFolderDelete()
        {
            var f = (ServiceId: 1, Title: "Title");
            var change = this.db!.ChangesDatabase.CreatePendingFolderDelete(f.ServiceId,
                                                                                   f.Title);
            Assert.NotEqual(0L, change.ChangeId);
            Assert.Equal(f.ServiceId, change.ServiceId);
            Assert.Equal(f.Title, change.Title);
        }

        [Fact]
        public void CanGetPendingFolderDeleteByChangeId()
        {
            var f = (ServiceId: 1, Title: "Title");
            var originalChange = this.db!.ChangesDatabase.CreatePendingFolderDelete(f.ServiceId, f.Title);
            var readChange = this.db!.ChangesDatabase.GetPendingFolderDelete(originalChange.ChangeId);

            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public void GettingNonExistentPendingFolderDeleteReturnsNull()
        {
            var change = this.db!.ChangesDatabase.GetPendingFolderDelete(99L);
            Assert.Null(change);
        }

        [Fact]
        public void CanRemovePendingFolderDeleteByChangeId()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            this.db!.ChangesDatabase.RemovePendingFolderDelete(change.ChangeId);
        }

        [Fact]
        public void RemovingNonExistentPendingFolderDeleteCompletesWithoutError()
        {
            this.db!.ChangesDatabase.RemovePendingFolderDelete(1L);
        }

        [Fact]
        public void RemovedPendingFolderDeleteIsActuallyRemoved()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            this.db!.ChangesDatabase.RemovePendingFolderDelete(change.ChangeId);

            var result = this.db!.ChangesDatabase.GetPendingFolderDelete(change.ChangeId);
            Assert.Null(result);

            var results = this.db!.ChangesDatabase.ListPendingFolderDeletes();
            Assert.Empty(results);
        }

        [Fact]
        public void CanListAllPendingFolderDeletes()
        {
            var changes = this.db!.ChangesDatabase;
            var change1 = changes.CreatePendingFolderDelete(1, "Title");
            var change2 = changes.CreatePendingFolderDelete(2, "Title2");

            var allChanges = changes.ListPendingFolderDeletes();
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public void ListingPendingFolderDeletesWithNoDeletesCompletesWithZeroResults()
        {
            var results = this.db!.ChangesDatabase.ListPendingFolderDeletes();
            Assert.Empty(results);
        }

        [Fact]
        public void AddingPendingFolderDeleteForUnreadFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderDelete(WellKnownServiceFolderIds.Unread, "Unread");
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingPendingFolderDeleteForArchiveFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderDelete(WellKnownServiceFolderIds.Archive, "Archive");
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingDuplicatePendingFolderDeleteShouldForServiceId()
        {
            _ = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            Assert.Throws<DuplicatePendingFolderDelete>(() => this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title2"));
        }

        [Fact]
        public void AddingDuplicatePendingFolderDeleteShouldForTitle()
        {
            _ = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            Assert.Throws<DuplicatePendingFolderDelete>(() => this.db!.ChangesDatabase.CreatePendingFolderDelete(98L, "Title"));
        }

        [Fact]
        public void AddingDuplicatePendingFolderDeleteShouldForServiceIdAndTitle()
        {
            _ = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            Assert.Throws<DuplicatePendingFolderDelete>(() => this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title"));
        }
        #endregion
    }
}