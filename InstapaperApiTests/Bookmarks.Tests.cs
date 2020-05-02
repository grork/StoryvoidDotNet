using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    /// <summary>
    /// Tests for the Bookmarks API are a little unique due to the limits the API
    /// puts in place for 'add' operations. To mitigate this, we only want to add
    /// when we actually have to add, and would rather manipulate existing bookmarks
    /// when possible.
    ///
    /// This results in some funky ordering, and spreading of state being built
    /// up as the tests execute in a specific order to slowly build up enough
    /// tested functionality to place the API in a known state
    /// </summary>
    [Order(4), Collection(TestUtilities.TestCollectionName)]
    public class BookmarksTests
    {
        private CurrentServiceStateFixture SharedState;

        public BookmarksTests(CurrentServiceStateFixture state)
        {
            this.SharedState = state;
        }

        [Fact, Order(1)]
        public async Task CoerceUserFolderBookmarksToOneFolder()
        {
            // Make sure there are no remote folders. We want to get all
            // the bookmarks we can into the unread wellknown folder so
            // we might have a chance of finding them
            if (this.SharedState.Folders.Count == 0)
            {
                var currentFolders = await this.SharedState.FoldersClient.List();
                this.SharedState.ReplaceFolderList(currentFolders);
            }

            foreach (var f in this.SharedState.Folders)
            {
                await this.SharedState.FoldersClient.Delete(f.FolderId);
            }

            this.SharedState.Folders.Clear();
        }

        [Fact, Order(2)]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var client = new BookmarksClient(TestUtilities.GetClientInformation());
            await client.List(WellKnownFolderIds.Unread);
        }
    }
}