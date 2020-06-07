using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class DatabaseTests
    {
        private static async Task<IArticleDatabase> GetDatabase()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var db = new ArticleDatabase(connection);
            await db.OpenOrCreateDatabaseAsync();

            return db;
        }

        [Fact]
        public async Task CanOpenDatabase()
        {
            using var db = await GetDatabase();
            Assert.NotNull(db);
        }

        [Fact]
        public async Task CanReopenDatabase()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            var first = new ArticleDatabase(connection);
            await first.OpenOrCreateDatabaseAsync();

            var second = new ArticleDatabase(connection);
            await first.OpenOrCreateDatabaseAsync();
        }

        [Fact]
        public async Task DefaultFoldersAreCreated()
        {
            using var db = await GetDatabase();
            IList<DatabaseFolder> result = await db.GetFoldersAsync();
            Assert.Equal(2, result.Count);

            Assert.Contains(result, (f) => f.ServiceId == WellKnownFolderIds.Unread);
            Assert.Contains(result, (f) => f.ServiceId == WellKnownFolderIds.Archive);
        }

        [Fact]
        public async Task CanGetSingleDefaultFolderByServiceId()
        {
            using var db = await GetDatabase();
            var folder = await db.GetFolderByServiceIdAsync(WellKnownFolderIds.Unread);

            Assert.NotNull(folder);
            Assert.Equal("Home", folder!.Title);
            Assert.Equal(WellKnownFolderIds.Unread, folder!.ServiceId);
            Assert.NotEqual(0L, folder!.LocalId);
        }

        [Fact]
        public async Task CanGetSingleDefaultFolderByLocalId()
        {
            using var db = await GetDatabase();
            var folder = await db.GetFolderByServiceIdAsync(WellKnownFolderIds.Unread);
            folder = await db.GetFolderByLocalIdAsync(folder!.LocalId);

            Assert.NotNull(folder);
            Assert.Equal("Home", folder!.Title);
            Assert.Equal(WellKnownFolderIds.Unread, folder!.ServiceId);
        }

        [Fact]
        public async Task GettingFolderThatDoesntExistByServiceIdDoesntReturnAnything()
        {
            using var db = await GetDatabase();
            var folder = await db.GetFolderByServiceIdAsync(1);

            Assert.Null(folder);
        }

        [Fact]
        public async Task GettingFolderThatDoesntExistByLocalIdDoesntReturnAnything()
        {
            using var db = await GetDatabase();
            var folder = await db.GetFolderByLocalIdAsync(5);

            Assert.Null(folder);
        }

        [Fact]
        public async Task CanAddFolder()
        {
            using var db = await GetDatabase();

            // Create folder; check results are returned
            var addedFolder = await db.CreateFolderAsync("Sample");
            Assert.Null(addedFolder.ServiceId);
            Assert.Equal("Sample", addedFolder.Title);
            Assert.NotEqual(0L, addedFolder.LocalId);

            // Request the folder explicitily, check it's data
            DatabaseFolder folder = (await db.GetFolderByLocalIdAsync(addedFolder.LocalId))!;
            Assert.Null(folder.ServiceId);
            Assert.Equal(addedFolder.Title, folder.Title);
            Assert.Equal(addedFolder.LocalId, folder.LocalId);

            // Check it comes back when listing all folders
            var allFolders = await db.GetFoldersAsync();
            Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
            Assert.Equal(3, allFolders.Count);
        }

        [Fact]
        public async Task AddingFolderWithDuplicateTitleFails()
        {
            using var db = await GetDatabase();

            // Create folder; then created it again, expecting it to fail
            _ = await db.CreateFolderAsync("Sample");
            _ = await Assert.ThrowsAsync<DuplicateNameException>(() => db.CreateFolderAsync("Sample"));

            // Check a spurious folder wasn't created
            var allFolders = await db.GetFoldersAsync();
            Assert.Equal(3, allFolders.Count);
        }
    }
}
