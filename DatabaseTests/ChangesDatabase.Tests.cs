using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class ChangesDatabaseTests : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private DatabaseFolder? CustomFolder1;
        private DatabaseFolder? CustomFolder2;

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();

            this.CustomFolder1 = await this.db.CreateFolderAsync("Sample1");
            this.CustomFolder2 = await this.db.CreateFolderAsync("Sample2");
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

        [Fact]
        public void CanCreatePendingFolderAdd()
        {
            var change = this.db!.PendingChangesDatabase.CreatePendingFolderAdd(this.CustomFolder1!.LocalId);
            Assert.NotEqual(0L, change.Id);
            Assert.Equal(this.CustomFolder1!.LocalId, change.FolderLocalId);
            Assert.Equal(this.CustomFolder1!.Title, change.Title);
        }

        [Fact]
        public async Task CanGetPendingFolderByChangeId()
        {
            var originalChange = this.db!.PendingChangesDatabase.CreatePendingFolderAdd(this.CustomFolder1!.LocalId);
            var readChange = await this.db!.PendingChangesDatabase.GetPendingFolderAddAsync(originalChange.Id);
            Assert.Equal(originalChange, readChange);
        }

        [Fact]
        public async Task CanListAllPendingFolderAdds()
        {
            var changes = this.db!.PendingChangesDatabase;
            var change1 = changes.CreatePendingFolderAdd(this.CustomFolder1!.LocalId);
            var change2 = changes.CreatePendingFolderAdd(this.CustomFolder2!.LocalId);

            var allChanges = await changes.ListPendingFolderAddsAsync();
            Assert.Equal(2, allChanges.Count);
            Assert.Contains(change1, allChanges);
            Assert.Contains(change2, allChanges);
        }
    }
}
