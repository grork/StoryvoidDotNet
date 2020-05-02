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
        }

        [Fact, Order(2)]
        public async Task AddingExistingFolderThrowsError()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            // Create the folder, but we don't need the result since we're
            // just going to add another one.
            _ = await client.Add(folderName);

            // Try adding the folder again, and expect a DuplicateFolderException
            _ = await Assert.ThrowsAsync<DuplicateFolderException>(() => client.Add(folderName));
        }

        [Fact, Order(3)]
        public async Task CanListFolders()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            var folders = await client.List();
            Assert.NotEmpty(folders); // Expected some elements, since we just created them

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
