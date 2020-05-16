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
        private IBookmarksClient Client => this.SharedState.BookmarksClient;

        public BookmarksTests(CurrentServiceStateFixture state)
        {
            this.SharedState = state;
        }

        [Fact, Order(1)]
        public async Task CanAddBookmark()
        {
            var bookmarkUrl = this.SharedState.GetNextAddableUrl();
            var result = await this.Client.Add(bookmarkUrl);

            Assert.Equal(bookmarkUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);

            this.SharedState.AddBookmark(result);
        }

        [Fact, Order(2)]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var remoteBookmarks = await this.Client.List(WellKnownFolderIds.Unread);
            this.SharedState.UpdateBookmarksForFolder(remoteBookmarks, WellKnownFolderIds.Unread);

            // Check the bookmark you had added most recently was found
            Assert.Contains(remoteBookmarks, (b) => b.Id == this.SharedState.RecentlyAddedBookmark!.Id);
        }

        [Fact, Order(3)]
        public async Task CanSuccessfullyAddDuplicateBookmark()
        {
            // Use a bookmark we just added
            var existingBookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.NotNull(existingBookmark); // Need existing bookmark

            var result = await this.Client.Add(existingBookmark.Url);
            Assert.Equal(existingBookmark.Id, result.Id);
            Assert.Equal(existingBookmark.Url, result.Url);
        }

        [Fact, Order(4)]
        public async Task CanSuccessfullyAddA404Bookmark()
        {
            // Adding URLs that don't exist is allowed by the API. However,
            // when trying to get the content (separate API), then it will fail
            var result = await this.Client.Add(this.SharedState.NonExistantUrl);
            Assert.Equal(this.SharedState.NonExistantUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);

            this.SharedState.AddBookmark(result);
        }

        [Fact]
        public async Task ExceptionThrownWithUnsupportedUriScheme()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => this.Client.Add(new Uri("ftp://something/foo.com")));
            await Assert.ThrowsAsync<ArgumentException>(() => this.Client.Add(new Uri("mailto:test@example.com")));
        }
    }
}