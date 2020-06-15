using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class FolderTests : IAsyncLifetime
    {
        private static void FoldersMatch(DatabaseFolder? folder1, DatabaseFolder? folder2)
        {
            Assert.NotNull(folder1);
            Assert.NotNull(folder2);

            Assert.Equal(folder1!.LocalId, folder2!.LocalId);
            Assert.Equal(folder1!.ServiceId, folder2!.ServiceId);
            Assert.Equal(folder1!.Title, folder2!.Title);
            Assert.Equal(folder1!.Position, folder2!.Position);
            Assert.Equal(folder1!.SyncToMobile, folder2!.SyncToMobile);
        }

        private IArticleDatabase? db;
        public async Task InitializeAsync() => this.db = await TestUtilities.GetDatabase();
        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task DefaultFoldersAreCreated()
        {
            IList<DatabaseFolder> result = await this.db!.GetFoldersAsync();
            Assert.Equal(2, result.Count);

            var unreadFolder = result.Where((f) => f.ServiceId == WellKnownFolderIds.Unread).First()!;
            var archiveFolder = result.Where((f) => f.ServiceId == WellKnownFolderIds.Archive).First()!;

            // Check the convenience IDs are correct
            Assert.Equal(unreadFolder.LocalId, this.db!.UnreadFolderLocalId);
            Assert.Equal(archiveFolder.LocalId, this.db!.ArchiveFolderLocalId);
        }

        [Fact]
        public async Task CanGetSingleDefaultFolderByServiceId()
        {
            var folder = await db!.GetFolderByServiceIdAsync(WellKnownFolderIds.Unread);

            Assert.NotNull(folder);
            Assert.Equal("Home", folder!.Title);
            Assert.Equal(WellKnownFolderIds.Unread, folder!.ServiceId);
            Assert.NotEqual(0L, folder!.LocalId);
        }

        [Fact]
        public async Task CanGetSingleDefaultFolderByLocalId()
        {
            var folder = await this.db!.GetFolderByServiceIdAsync(WellKnownFolderIds.Unread);
            folder = await this.db!.GetFolderByLocalIdAsync(folder!.LocalId);

            Assert.NotNull(folder);
            Assert.Equal("Home", folder!.Title);
            Assert.Equal(WellKnownFolderIds.Unread, folder!.ServiceId);
        }

        [Fact]
        public async Task GettingFolderThatDoesntExistByServiceIdDoesntReturnAnything()
        {
            var folder = await this.db!.GetFolderByServiceIdAsync(1);

            Assert.Null(folder);
        }

        [Fact]
        public async Task GettingFolderThatDoesntExistByLocalIdDoesntReturnAnything()
        {
            var folder = await this.db!.GetFolderByLocalIdAsync(5);

            Assert.Null(folder);
        }

        [Fact]
        public async Task CanAddFolder()
        {
            // Create folder; check results are returned
            var addedFolder = await this.db!.CreateFolderAsync("Sample");
            Assert.Null(addedFolder.ServiceId);
            Assert.Equal("Sample", addedFolder.Title);
            Assert.NotEqual(0L, addedFolder.LocalId);

            // Request the folder explicitily, check it's data
            DatabaseFolder folder = (await this.db!.GetFolderByLocalIdAsync(addedFolder.LocalId))!;
            FoldersMatch(addedFolder, folder);

            // Check it comes back when listing all folders
            var allFolders = await this.db!.GetFoldersAsync();
            Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
            Assert.Equal(3, allFolders.Count);
        }

        [Fact]
        public async Task CanAddMultipleFolders()
        {
            // Create folder; check results are returned
            var addedFolder = await this.db!.CreateFolderAsync("Sample");
            Assert.Null(addedFolder.ServiceId);
            Assert.Equal("Sample", addedFolder.Title);
            Assert.NotEqual(0L, addedFolder.LocalId);

            var addedFolder2 = await this.db!.CreateFolderAsync("Sample2");
            Assert.Null(addedFolder2.ServiceId);
            Assert.Equal("Sample2", addedFolder2.Title);
            Assert.NotEqual(0L, addedFolder2.LocalId);

            // Check it comes back when listing all folders
            var allFolders = await this.db!.GetFoldersAsync();
            Assert.Contains(allFolders, (f) => f.LocalId == addedFolder.LocalId);
            Assert.Equal(4, allFolders.Count);
        }

        [Fact]
        public async Task AddingFolderWithDuplicateTitleFails()
        {
            // Create folder; then created it again, expecting it to fail
            _ = await this.db!.CreateFolderAsync("Sample");
            _ = await Assert.ThrowsAsync<DuplicateNameException>(() => this.db!.CreateFolderAsync("Sample"));

            // Check a spurious folder wasn't created
            var allFolders = await this.db!.GetFoldersAsync();
            Assert.Equal(3, allFolders.Count);
        }

        [Fact]
        public async Task CanAddFolderWithAllServiceInformation()
        {
            // Create folder; check results are returned
            DatabaseFolder addedFolder = await this.db!.AddKnownFolderAsync(
                title: "Sample",
                serviceId: 10L,
                position: 9L,
                syncToMobile: true
            );

            Assert.Equal(10L, addedFolder.ServiceId);
            Assert.Equal("Sample", addedFolder.Title);
            Assert.NotEqual(0L, addedFolder.LocalId);
            Assert.Equal(9L, addedFolder.Position);
            Assert.True(addedFolder.SyncToMobile);

            // Request the folder explicitily, check it's data
            DatabaseFolder folder = (await this.db!.GetFolderByLocalIdAsync(addedFolder.LocalId))!;
            FoldersMatch(addedFolder, folder);

            // Check it comes back when listing all folders
            var allFolders = await this.db!.GetFoldersAsync();
            var folderFromList = allFolders.Where((f) => f.LocalId == addedFolder.LocalId).FirstOrDefault();
            FoldersMatch(addedFolder, folderFromList);
        }

        [Fact]
        public async Task AddFolderDuplicateTitleUsingServiceInformationThrows()
        {
            // Create folder; check results are returned
            var addedFolder = await this.db!.AddKnownFolderAsync(
                title: "Sample",
                serviceId: 10L,
                position: 9L,
                syncToMobile: true
            );

            _ = await Assert.ThrowsAsync<DuplicateNameException>(() => this.db!.AddKnownFolderAsync(
                title: addedFolder.Title,
                serviceId: addedFolder.ServiceId!.Value,
                position: addedFolder.Position,
                syncToMobile: addedFolder.SyncToMobile
            ));

            // Check it comes back when listing all folders
            var allFolders = await this.db!.GetFoldersAsync();
            Assert.Equal(3, allFolders.Count);
        }

        [Fact]
        public async Task CanUpdateAddedFolderWithFullSetOfInformation()
        {
            // Create local only folder
            var folder = await this.db!.CreateFolderAsync("Sample");
            Assert.Null(folder.ServiceId);
            Assert.False(folder.IsOnService);

            // Update the local only folder with additional data
            DatabaseFolder updatedFolder = await db.UpdateFolderAsync(
                localId: folder.LocalId,
                serviceId: 9L,
                title: "Sample2",
                position: 999L,
                syncToMobile: false
            );

            // Should be the same folder
            Assert.Equal(folder.LocalId, updatedFolder.LocalId);

            // Values we updated should be reflected
            Assert.NotNull(updatedFolder.ServiceId);
            Assert.Equal(9L, updatedFolder.ServiceId);
            Assert.True(updatedFolder.IsOnService);

            Assert.Equal("Sample2", updatedFolder.Title);
            Assert.Equal(999L, updatedFolder.Position);
            Assert.False(updatedFolder.SyncToMobile);
        }

        [Fact]
        public async Task UpdatingFolderThatDoesntExistFails()
        {
            await Assert.ThrowsAsync<FolderNotFoundException>(async () =>
            {
                _ = await db!.UpdateFolderAsync(
                    localId: 9,
                    serviceId: 9L,
                    title: "Sample2",
                    position: 999L,
                    syncToMobile: false
                );
            });
        }

        [Fact]
        public async Task CanDeleteEmptyFolder()
        {
            // Create folder; check results are returned
            var addedFolder = await this.db!.AddKnownFolderAsync(
                title: "Sample",
                serviceId: 10L,
                position: 9L,
                syncToMobile: true
            );

            await this.db!.DeleteFolderAsync(addedFolder.LocalId);

            // Verify folder is missing
            var folders = await this.db!.GetFoldersAsync();
            Assert.Equal(2, folders.Count);
        }

        [Fact]
        public async Task DeletingMissingFolderNoOps()
        {
            await this.db!.DeleteFolderAsync(999);
        }

        [Fact]
        public async Task DeletingUnreadFolderThrows()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => this.db!.DeleteFolderAsync(this.db!.UnreadFolderLocalId));
        }

        [Fact]
        public async Task DeletingArchiveFolderThrows()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => this.db!.DeleteFolderAsync(this.db!.UnreadFolderLocalId));
        }
    }
}
