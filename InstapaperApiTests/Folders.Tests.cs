using System;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    [Order(2), Collection(TestUtilities.TestCollectionName)]
    public class FoldersTests
    {
        private CurrentServiceStateFixture SharedState;
        public FoldersTests(CurrentServiceStateFixture state)
        {
            this.SharedState = state;
        }

        [Fact, Order(1)]
        public async Task CanAddFolder()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            var createdFolder = await client.Add(folderName);
            Assert.Equal(folderName, createdFolder.Title);
            Assert.True(createdFolder.SyncToMobile);
            Assert.InRange(createdFolder.Position, 1UL, ulong.MaxValue);
            Assert.InRange(createdFolder.FolderId, 1UL, ulong.MaxValue);

            this.SharedState.Folders.Add(createdFolder);
        }

        [Fact, Order(2)]
        public async Task AddingExistingFolderThrowsError()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            // Create the folder, but we don't need the result since we're
            // just going to add another one.
            var createdFolder = await client.Add(folderName);
            this.SharedState.Folders.Add(createdFolder);

            // Try adding the folder again, and expect a DuplicateFolderException
            await Assert.ThrowsAsync<DuplicateFolderException>(() => client.Add(folderName));
        }

        [Fact, Order(3)]
        public async Task CanListFolders()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            var folders = await client.List();
            Assert.NotEmpty(folders); // Expected some elements, since we just created them

            // Check that all the values are correct
            Assert.All(folders, (folder) =>
            {
                Assert.NotEmpty(folder.Title);
                Assert.True(folder.SyncToMobile);
                Assert.InRange(folder.Position, 1UL, ulong.MaxValue);
                Assert.InRange(folder.FolderId, 1UL, ulong.MaxValue);
            });

            // Check that the folders we thought we'd created earlier are
            // present.
            //
            // Yes, this is a bit weird to assess the creation at this point
            // rather than in the actual creation test, but given listing
            // requires user folders to be present which requires add to also
            // be functional, this seems like a sensible trade off.
            foreach (var f in this.SharedState.Folders)
            {
                Assert.Contains(folders, (folder) => folder.FolderId == f.FolderId);
            }

            this.SharedState.ReplaceFolderList(folders);
        }

        [Fact, Order(4)]
        public void FolderIdLessThanOneThrows()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Delete(0));
        }

        [Fact, Order(5)]
        public async Task CanDeleteFolder()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            // Get the first folder from the shared state, and try to delete it
            var folderToDelete = this.SharedState.Folders.First();

            await client.Delete(folderToDelete.FolderId);

            // List the remote folders and check it was actually deleted
            var folders = await client.List();
            this.SharedState.ReplaceFolderList(folders);

            Assert.DoesNotContain(folders, (folder) => (folderToDelete.Title == folder.Title));
        }

        [Fact, Order(6)]
        public async Task DeletingFolderThatDoesntExistThrows()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            await Assert.ThrowsAsync<UnknownServiceError>(() => client.Delete(42));
        }
    }
}
