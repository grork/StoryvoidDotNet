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
            var changesDb = this.db!.PendingChangesDatabase;
            Assert.NotNull(changesDb);
        }

#region Pending Folder Adds
        [Fact]
        public void CanCreatePendingFolderAdd()
        {
            var change = this.db!.PendingChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            Assert.NotEqual(0L, change.ChangeId);
            Assert.Equal(this.CustomLocalFolder1!.LocalId, change.FolderLocalId);
            Assert.Equal(this.CustomLocalFolder1!.Title, change.Title);
        }

        [Fact]
        public async Task CanGetPendingFolderAddByChangeId()
        {
            var originalChange = this.db!.PendingChangesDatabase.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var readChange = await this.db!.PendingChangesDatabase.GetPendingFolderAddAsync(originalChange.ChangeId);
            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public async Task CanListAllPendingFolderAdds()
        {
            var changes = this.db!.PendingChangesDatabase;
            var change1 = changes.CreatePendingFolderAdd(this.CustomLocalFolder1!.LocalId);
            var change2 = changes.CreatePendingFolderAdd(this.CustomLocalFolder2!.LocalId);

            var allChanges = await changes.ListPendingFolderAddsAsync();
            Assert.Equal(2, allChanges.Count);
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public async Task ListingWithNoAddsCompletesWithZeroResults()
        {
            var results = await this.db!.PendingChangesDatabase.ListPendingFolderAddsAsync();
            Assert.Empty(results);
        }
#endregion

#region Pending Folder Deletes
        [Fact]
        public void CanCreatePendingFolderDelete()
        {
            var f = (ServiceId: 1, Title: "Title");
            var change = this.db!.PendingChangesDatabase.CreatePendingFolderDelete(f.ServiceId,
                                                                                   f.Title);
            Assert.NotEqual(0L, change.ChangeId);
            Assert.Equal(f.ServiceId, change.ServiceId);
            Assert.Equal(f.Title, change.Title);
        }

        [Fact]
        public async Task CanGetPendingFolderDeleteByChangeId()
        {
            var f = (ServiceId: 1, Title: "Title");
            var originalChange = this.db!.PendingChangesDatabase.CreatePendingFolderDelete(f.ServiceId, f.Title);
            var readChange = await this.db!.PendingChangesDatabase.GetPendingFolderDeleteAsync(originalChange.ChangeId);

            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public async Task CanListAllPendingFolderDeletes()
        {
            var changes = this.db!.PendingChangesDatabase;
            var change1 = changes.CreatePendingFolderDelete(1, "Title");
            var change2 = changes.CreatePendingFolderDelete(2, "Title2");

            var allChanges = await changes.ListPendingFolderDeletesAsync();
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }

        [Fact]
        public async Task ListingWithNoDeletesCompletesWithZeroResults()
        {
            var results = await this.db!.PendingChangesDatabase.ListPendingFolderDeletesAsync();
            Assert.Empty(results);
        }
#endregion
    }
}