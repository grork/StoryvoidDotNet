using System;
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
    [Order(2), Collection(TestUtilities.TestCollectionName)]
    public class BookmarksTests
    {
        private CurrentServiceStateFixture SharedState;

        public BookmarksTests(CurrentServiceStateFixture state)
        {
            this.SharedState = state;
        }

        [Fact, Order(1)]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var client = this.SharedState.BookmarksClient;
            var remoteBookmarks = await client.List(WellKnownFolderIds.Unread);
            this.SharedState.UpdateBookmarksForFolder(remoteBookmarks, WellKnownFolderIds.Unread);
        }

        [Fact]
        public async Task ExceptionThrownWithOutHttpUrl()
        {
            var client = this.SharedState.BookmarksClient;
            await Assert.ThrowsAsync<ArgumentException>(() => client.Add(new Uri("ftp://something/foo.com")));
            await Assert.ThrowsAsync<ArgumentException>(() => client.Add(new Uri("mailto:test@example.com")));
        }
    }
}