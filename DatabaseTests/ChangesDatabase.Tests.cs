using System;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class ChangesDatabaseTests : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private DatabaseFolder? CustomLocalFolder1;
        private DatabaseFolder? CustomLocalFolder2;

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();

            this.CustomLocalFolder1 = await this.db.CreateFolderAsync("LocalSample1");
            this.CustomLocalFolder2 = await this.db.CreateFolderAsync("LocalSample2");
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
        public async Task CanGetPendingFolderAddByChangeId()
        {
            var originalChange = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var readChange = await this.db!.ChangesDatabase.GetPendingFolderAddAsync(originalChange.ChangeId);
            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public async Task GettingNonExistentPendingFolderAddReturnsNull()
        {
            var change = await this.db!.ChangesDatabase.GetPendingFolderAddAsync(99L);
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
        public async Task RemovedPendingFolderAddIsActuallyRemoved()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            this.db!.ChangesDatabase.RemovePendingFolderAdd(change.ChangeId);

            var result = await this.db!.ChangesDatabase.GetPendingFolderAddAsync(change.ChangeId);
            Assert.Null(result);

            var results = await this.db!.ChangesDatabase.ListPendingFolderAddsAsync();
            Assert.Empty(results);
        }

        [Fact]
        public async Task CanListAllPendingFolderAdds()
        {
            var changes = this.db!.ChangesDatabase;
            var change1 = changes.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var change2 = changes.CreatePendingFolderAdd(this.CustomLocalFolder2!.LocalId);

            var allChanges = await changes.ListPendingFolderAddsAsync();
            Assert.Equal(2, allChanges.Count);
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public async Task ListingPendingFolderAddsWithNoAddsCompletesWithZeroResults()
        {
            var results = await this.db!.ChangesDatabase.ListPendingFolderAddsAsync();
            Assert.Empty(results);
        }

        [Fact]
        public void AddingPendingFolderAddForUnreadFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderAdd(this.db!.UnreadFolderLocalId);
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingPendingFolderAddForArchiveFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderAdd(this.db!.ArchiveFolderLocalId);
            }

            Assert.Throws<InvalidOperationException>(Work);
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
        public async Task CanGetPendingFolderDeleteByChangeId()
        {
            var f = (ServiceId: 1, Title: "Title");
            var originalChange = this.db!.ChangesDatabase.CreatePendingFolderDelete(f.ServiceId, f.Title);
            var readChange = await this.db!.ChangesDatabase.GetPendingFolderDeleteAsync(originalChange.ChangeId);

            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public async Task GettingNonExistentPendingFolderDeleteReturnsNull()
        {
            var change = await this.db!.ChangesDatabase.GetPendingFolderDeleteAsync(99L);
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
        public async Task RemovedPendingFolderDeleteIsActuallyRemoved()
        {
            var change = this.db!.ChangesDatabase.CreatePendingFolderDelete(99L, "Title");
            this.db!.ChangesDatabase.RemovePendingFolderDelete(change.ChangeId);

            var result = await this.db!.ChangesDatabase.GetPendingFolderDeleteAsync(change.ChangeId);
            Assert.Null(result);

            var results = await this.db!.ChangesDatabase.ListPendingFolderDeletesAsync();
            Assert.Empty(results);
        }

        [Fact]
        public async Task CanListAllPendingFolderDeletes()
        {
            var changes = this.db!.ChangesDatabase;
            var change1 = changes.CreatePendingFolderDelete(1, "Title");
            var change2 = changes.CreatePendingFolderDelete(2, "Title2");

            var allChanges = await changes.ListPendingFolderDeletesAsync();
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public async Task ListingPendingFolderDeletesWithNoDeletesCompletesWithZeroResults()
        {
            var results = await this.db!.ChangesDatabase.ListPendingFolderDeletesAsync();
            Assert.Empty(results);
        }

        [Fact]
        public void AddingPendingFolderDeleteForUnreadFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderDelete(WellKnownFolderIds.Unread, "Unread");
            }

            Assert.Throws<InvalidOperationException>(Work);
        }

        [Fact]
        public void AddingPendingFolderDeleteForArchiveFolderShouldFail()
        {
            void Work()
            {
                this.db!.ChangesDatabase.CreatePendingFolderDelete(WellKnownFolderIds.Archive, "Archive");
            }

            Assert.Throws<InvalidOperationException>(Work);
        }
        #endregion
    }
}