using Codevoid.Instapaper;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper;

[Order(1), Collection(TestUtilities.TestCollectionName)]
public sealed class FoldersTests
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
        var client = this.SharedState.FoldersClient;

        var createdFolder = await client.AddAsync(folderName);
        Assert.Equal(folderName, createdFolder.Title);
        Assert.True(createdFolder.SyncToMobile);
        Assert.InRange(createdFolder.Position, 1L, long.MaxValue);
        Assert.InRange(createdFolder.Id, 1L, long.MaxValue);

        this.SharedState.UpdateOrSetRecentFolder(createdFolder);
    }

    [Fact, Order(2)]
    public async Task AddingExistingFolderThrowsError()
    {
        var client = this.SharedState.FoldersClient;

        // Try adding the folder again, and expect a DuplicateFolderException
        await Assert.ThrowsAsync<DuplicateFolderException>(() => client.AddAsync(this.SharedState.RecentlyAddedFolder!.Title));
    }

    [Fact, Order(3)]
    public async Task CanListFolders()
    {
        var client = this.SharedState.FoldersClient;
        var folders = await client.ListAsync();
        Assert.NotEmpty(folders); // Expected some elements, since we just created them

        // Check that all the values are correct
        Assert.All(folders, (folder) =>
        {
            Assert.NotEmpty(folder.Title);
            Assert.True(folder.SyncToMobile);
            Assert.InRange(folder.Position, 1L, long.MaxValue);
            Assert.InRange(folder.Id, 1L, long.MaxValue);
        });

        // Check that the folder we'd added recently is in the list
        Assert.Contains(folders, (IInstapaperFolder f) => this.SharedState.RecentlyAddedFolder!.Id == f.Id);
    }

    [Fact, Order(4)]
    public void FolderIdLessThanOneThrows()
    {
        var client = this.SharedState.FoldersClient;
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.DeleteAsync(0));
    }

    [Fact, Order(5)]
    public async Task CanDeleteFolder()
    {
        var client = this.SharedState.FoldersClient;

        // Get the first folder from the shared state, and try to delete it
        var folderToDelete = this.SharedState.RecentlyAddedFolder!;
        await client.DeleteAsync(folderToDelete.Id);

        // List the remote folders and check it was actually deleted
        var folders = await client.ListAsync();
        Assert.DoesNotContain(folders, (folder) => (folderToDelete.Title == folder.Title));

        // We deleted a folder, so clear it from our recent ones
        this.SharedState.UpdateOrSetRecentFolder(null);
    }

    [Fact, Order(6)]
    public async Task DeletingFolderThatDoesntExistThrows()
    {
        var client = this.SharedState.FoldersClient;
        await Assert.ThrowsAsync<UnknownServiceError>(() => client.DeleteAsync(42));
    }
}