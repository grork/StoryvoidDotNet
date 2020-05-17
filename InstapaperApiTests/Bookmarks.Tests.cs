using System;
using System.Linq;
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

        [Fact]
        public async Task AddingBookmarkUnsupportedUriSchemeThrows()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await this.Client.Add(new Uri("ftp://something/foo.com")));
            await Assert.ThrowsAsync<ArgumentException>(async () => await this.Client.Add(new Uri("mailto:test@example.com")));
        }

        [Fact, Order(1)]
        public async Task CanAddBookmark()
        {
            var bookmarkUrl = this.SharedState.GetNextAddableUrl();
            var result = await this.Client.Add(bookmarkUrl);

            Assert.Equal(bookmarkUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);

            this.SharedState.UpdateOrSetRecentBookmark(result);
        }

        [Fact, Order(2)]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var remoteBookmarks = await this.Client.List(WellKnownFolderIds.Unread);

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
        }

        [Fact]
        public async Task UpdatingProgressWithoutBookmarkIdThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgress(0, 0.0, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithNegativeProgressThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgress(1, -0.5, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithProgressGreaterThan1Throws()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgress(1, 1.1, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithTimeStampBeforeUnixEpochThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgress(1, 0.0, new DateTime(1956, 2, 11)));
        }

        [Fact, Order(5)]
        public async Task CanExplicitlyUpdateProgressForBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var updateTime = DateTime.Now;
            var progress = bookmark.Progress + 0.1;

            // Progress needs to be clamped between 0.0 and 1.0. Instead of
            // setting a specific value and not being sure if it changed,
            // check if we've gone to far, and roll it back around again
            if (progress > 1.0)
            {
                progress = 0.1;
            }

            var result = await this.Client.UpdateReadProgress(bookmark.Id, progress, updateTime);
            Assert.Equal(bookmark.Id, result.Id);
            Assert.Equal(progress, result.Progress);
            // We lose precision in the conversion to unix epoc, so give it some
            // wiggle room (300ms) on the equality check
            Assert.Equal(updateTime, result.ProgressTimestamp, TimeSpan.FromMilliseconds(300));

            this.SharedState.UpdateOrSetRecentBookmark(result);
        }

        [Fact, Order(6)]
        public async Task CanListLikedFolderAndItIsEmpty()
        {
            var result = await this.Client.List(WellKnownFolderIds.Liked);
            Assert.Empty(result); // Didn't expect any bookmarks in this folder
        }

        [Fact, Order(7)]
        public async Task CanLikeBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.False(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.Like(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.True(result.Liked); // Bookmark should be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var likedBookmarks = await this.Client.List(WellKnownFolderIds.Liked);
            Assert.Equal(1, likedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, likedBookmarks.First().Id);
        }

        [Fact, Order(8)]
        public async Task CanLikeBookmarkThatIsAlreadyLiked()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.True(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.Like(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.True(result.Liked); // Bookmark should be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var likedBookmarks = await this.Client.List(WellKnownFolderIds.Liked);
            Assert.Equal(1, likedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, likedBookmarks.First().Id);
        }

        [Fact, Order(9)]
        public async Task CanUnlikeLikedBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.True(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.Unlike(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.False(result.Liked); // Bookmark should not be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var likedBookmarks = await this.Client.List(WellKnownFolderIds.Liked);
            Assert.Empty(likedBookmarks); // Didn't expect any bookmarks
        }

        [Fact, Order(10)]
        public async Task CanUnlikeBookmarkThatIsNotLiked()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.False(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.Unlike(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.False(result.Liked); // Bookmark should not be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var likedBookmarks = await this.Client.List(WellKnownFolderIds.Liked);
            Assert.Empty(likedBookmarks); // Didn't expect any bookmarks
        }

        [Fact, Order(11)]
        public async Task CanListArchiveFolderAndItIsEmpty()
        {
            var result = await this.Client.List(WellKnownFolderIds.Archived);
            Assert.Empty(result); // Didn't expect any bookmarks in this folder
        }

        [Fact, Order(12)]
        public async Task CanArchiveBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var result = await this.Client.Archive(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var archivedBookmarks = await this.Client.List(WellKnownFolderIds.Archived);
            Assert.Equal(1, archivedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, archivedBookmarks.First().Id);
        }

        [Fact, Order(13)]
        public async Task CanArchiveBookmarkThatIsAlreadyArchived()
        {
            await this.CanArchiveBookmark();
        }

        [Fact, Order(14)]
        public async Task CanUnarchiveArchivedBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var result = await this.Client.Unarchive(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var archivedBookmarks = await this.Client.List(WellKnownFolderIds.Archived);
            Assert.Empty(archivedBookmarks); // Only expected one bookmark
        }

        [Fact, Order(15)]
        public async Task CanUnarchiveANonArchivedBookmark()
        {
            await this.CanUnarchiveArchivedBookmark();
        }
    }
}