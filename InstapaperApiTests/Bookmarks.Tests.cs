using System;
using System.Collections.Generic;
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
    public sealed class BookmarksTests
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
            await Assert.ThrowsAsync<ArgumentException>(async () => await this.Client.AddAsync(new Uri("ftp://something/foo.com")));
            await Assert.ThrowsAsync<ArgumentException>(async () => await this.Client.AddAsync(new Uri("mailto:test@example.com")));
        }

        [Fact, Order(1)]
        public async Task CanAddBookmark()
        {
            var bookmarkUrl = this.SharedState.GetNextAddableUrl();
            var result = await this.Client.AddAsync(bookmarkUrl);

            Assert.Equal(bookmarkUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);
            Assert.False(String.IsNullOrWhiteSpace(result.Hash));

            this.SharedState.UpdateOrSetRecentBookmark(result);
        }

        [Fact, Order(2)]
        public async Task CanAddBookmarkWithOptions()
        {
            var bookmarkUrl = this.SharedState.RecentlyAddedBookmark!.Url;
            var options = new AddBookmarkOptions()
            {
                Title = "Sample Title",
                Description = "Sample Description",
                ResolveToFinalUrl = false
            };

            var result = await this.Client.AddAsync(bookmarkUrl, options);

            Assert.Equal(bookmarkUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);
            Assert.False(String.IsNullOrWhiteSpace(result.Hash));
            Assert.Equal(options.Title, result.Title);
            Assert.Equal(options.Description, result.Description);

            this.SharedState.UpdateOrSetRecentBookmark(result);
        }

        [Fact, Order(3)]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var (remoteBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Unread);

            // Check the bookmark you had added most recently was found
            Assert.Contains(remoteBookmarks, (b) => b.Id == this.SharedState.RecentlyAddedBookmark!.Id);
        }

        [Fact, Order(4)]
        public async Task CanSuccessfullyAddDuplicateBookmark()
        {
            // Use a bookmark we just added
            var existingBookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.NotNull(existingBookmark); // Need existing bookmark

            var result = await this.Client.AddAsync(existingBookmark.Url);
            Assert.Equal(existingBookmark.Id, result.Id);
            Assert.Equal(existingBookmark.Url, result.Url);
        }

        [Fact, Order(5)]
        public async Task CanSuccessfullyAddA404Bookmark()
        {
            // Adding URLs that don't exist is allowed by the API. However,
            // when trying to get the content (separate API), then it will fail
            var result = await this.Client.AddAsync(this.SharedState.NonExistantUrl);
            Assert.Equal(this.SharedState.NonExistantUrl, result.Url);
            Assert.NotEqual(0UL, result.Id);

            // Make sure we keep this bookmark for later use
            this.SharedState.SetNotFoundBookmark(result);
        }

        [Fact]
        public async Task UpdatingProgressWithoutBookmarkIdThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgressAsync(0, 0.0F, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithNegativeProgressThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgressAsync(1, -0.5F, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithProgressGreaterThan1Throws()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgressAsync(1, 1.1F, DateTime.Now));
        }

        [Fact]
        public async Task UpdatingProgressWithTimeStampBeforeUnixEpochThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.UpdateReadProgressAsync(1, 0.0F, new DateTime(1956, 2, 11)));
        }

        [Fact, Order(6)]
        public async Task CanExplicitlyUpdateProgressForBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var updateTime = DateTime.Now;
            var progress = bookmark.Progress + 0.1F;

            // Progress needs to be clamped between 0.0 and 1.0. Instead of
            // setting a specific value and not being sure if it changed,
            // check if we've gone to far, and roll it back around again
            if (progress > 1.0F)
            {
                progress = 0.1F;
            }

            var result = await this.Client.UpdateReadProgressAsync(bookmark.Id, progress, updateTime);
            Assert.Equal(bookmark.Id, result.Id);
            Assert.Equal(progress, result.Progress);
            // We lose precision in the conversion to unix epoc, so give it some
            // wiggle room (300ms) on the equality check
            Assert.Equal(updateTime, result.ProgressTimestamp, TimeSpan.FromMilliseconds(300));

            this.SharedState.UpdateOrSetRecentBookmark(result);
        }

        [Fact]
        public async Task UpdatingProgressForNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.UpdateReadProgressAsync(1UL, 0.5F, DateTime.Now));
        }

        [Fact, Order(7)]
        public async Task CanListLikedFolderAndItIsEmpty()
        {
            var (result, _) = await this.Client.ListAsync(WellKnownFolderIds.Liked);
            Assert.Empty(result); // Didn't expect any bookmarks in this folder
        }

        [Fact, Order(8)]
        public async Task CanLikeBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.False(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.LikeAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.True(result.Liked); // Bookmark should be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (likedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Liked);
            Assert.Equal(1, likedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, likedBookmarks.First().Id);
        }

        [Fact, Order(9)]
        public async Task CanLikeBookmarkThatIsAlreadyLiked()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.True(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.LikeAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.True(result.Liked); // Bookmark should be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (likedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Liked);
            Assert.Equal(1, likedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, likedBookmarks.First().Id);
        }

        [Fact, Order(10)]
        public async Task CanUnlikeLikedBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.True(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.UnlikeAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.False(result.Liked); // Bookmark should not be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (likedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Liked);
            Assert.Empty(likedBookmarks); // Didn't expect any bookmarks
        }

        [Fact, Order(11)]
        public async Task CanUnlikeBookmarkThatIsNotLiked()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            Assert.False(bookmark.Liked); // Need a non-liked bookmark

            var result = await this.Client.UnlikeAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            Assert.False(result.Liked); // Bookmark should not be liked now

            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (likedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Liked);
            Assert.Empty(likedBookmarks); // Didn't expect any bookmarks
        }

        [Fact]
        public async Task LikingNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.LikeAsync(1UL));
        }

        [Fact]
        public async Task UnlikingNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.UnlikeAsync(1UL));
        }

        [Fact, Order(12)]
        public async Task CanListArchiveFolderAndItIsEmpty()
        {
            var (result, _) = await this.Client.ListAsync(WellKnownFolderIds.Archived);
            Assert.Empty(result); // Didn't expect any bookmarks in this folder
        }

        [Fact, Order(13)]
        public async Task CanArchiveBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var result = await this.Client.ArchiveAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (archivedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Archived);
            Assert.Equal(1, archivedBookmarks.Count); // Only expected one bookmark

            // Check the one we JUST added is actually present
            Assert.Equal(result.Id, archivedBookmarks.First().Id);
        }

        [Fact, Order(14)]
        public async Task CanArchiveBookmarkThatIsAlreadyArchived()
        {
            await this.CanArchiveBookmark();
        }

        [Fact, Order(15)]
        public async Task CanUnarchiveArchivedBookmark()
        {
            var bookmark = this.SharedState.RecentlyAddedBookmark!;
            var result = await this.Client.UnarchiveAsync(bookmark.Id);

            Assert.Equal(bookmark.Id, result.Id);
            this.SharedState.UpdateOrSetRecentBookmark(result);

            // Check that it is actually liked in the listing
            var (archivedBookmarks, _) = await this.Client.ListAsync(WellKnownFolderIds.Archived);
            Assert.Empty(archivedBookmarks); // Only expected one bookmark
        }

        [Fact, Order(16)]
        public async Task CanUnarchiveANonArchivedBookmark()
        {
            await this.CanUnarchiveArchivedBookmark();
        }

        [Fact]
        public async Task ArchivingNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.ArchiveAsync(1UL));
        }

        [Fact]
        public async Task UnarchiveNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.UnarchiveAsync(1UL));
        }

        [Fact, Order(17)]
        public async Task CanMoveBookmarkToFolderById()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var folder = await this.SharedState.FoldersClient.AddAsync(folderName);
            this.SharedState.UpdateOrSetRecentFolder(folder);

            var client = this.SharedState.BookmarksClient;
            var bookmarkToMove = this.SharedState.RecentlyAddedBookmark!;
            var movedBookmark = await client.MoveAsync(bookmarkToMove.Id, folder.Id);
            Assert.Equal(bookmarkToMove.Id, movedBookmark.Id);

            // Check that the bookmark is in the remote folder
            var (folderContents, _) = await client.ListAsync(folder.Id);
            Assert.Contains(folderContents, (b) => b.Id == movedBookmark.Id);

            this.SharedState.UpdateOrSetRecentBookmark(movedBookmark);
        }

        [Fact, Order(18)]
        public async Task MovingNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.MoveAsync(1UL, this.SharedState.RecentlyAddedFolder!.Id));
        }

        [Fact, Order(19)]
        public async Task CanAddBookmarkDirectlyToFolder()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            var candidate = this.SharedState.NotFoundBookmark;
            var options = new AddBookmarkOptions() { DestinationFolderId = folder.Id };
            _ = await client.AddAsync(candidate!.Url, options);

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            Assert.Contains(folderContent, (b) => b.Id == candidate.Id);
        }

        [Fact, Order(20)]
        public async Task CanUpdateProgressViaListWithHave()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            var bookmark1 = folderContent[0];
            var bookmark2 = folderContent[1];

            // Calculate new progress for bookmark 1
            var bookmark1ProgressTimestamp = DateTime.Now;
            var bookmark1Progress = bookmark1.Progress + 0.1F;
            if (bookmark1Progress > 1.0F)
            {
                bookmark1Progress = 0.1F;
            }

            // Calculate new progress for bookmark 2
            var bookmark2ProgressTimestamp = DateTime.Now;
            var bookmark2Progress = bookmark2.Progress + 0.1F;
            if (bookmark2Progress > 1.0F)
            {
                bookmark2Progress = 0.1F;
            }

            // Create Have Items. Note that we are sending up a random hash
            // value to ensure that the service returns back the changes
            var bookmark1Have = new HaveStatus(bookmark1.Id, "X", bookmark1Progress, bookmark1ProgressTimestamp);
            var bookmark2Have = new HaveStatus(bookmark2.Id, "X", bookmark2Progress, bookmark2ProgressTimestamp);

            // Perform the list with the have information
            var (updatedBookmarks, _) = await client.ListAsync(folder.Id, new[] { bookmark1Have, bookmark2Have });
            Assert.Equal(2, updatedBookmarks.Count); // Expected two bookmarks

            var updatedBookmark1 = (from b in updatedBookmarks
                                    where b.Id == bookmark1.Id
                                    select b).First();

            var updatedBookmark2 = (from b in updatedBookmarks
                                    where b.Id == bookmark2.Id
                                    select b).First();

            // Check progress has been updated
            Assert.Equal(bookmark1Progress, updatedBookmark1.Progress);
            Assert.Equal(bookmark2Progress, updatedBookmark2.Progress);

            // Check that the timestamps match
            Assert.Equal(bookmark1ProgressTimestamp, updatedBookmark1.ProgressTimestamp, TimeSpan.FromMilliseconds(300));
            Assert.Equal(bookmark2ProgressTimestamp, updatedBookmark2.ProgressTimestamp, TimeSpan.FromMilliseconds(300));
        }

        [Fact, Order(21)]
        public async Task ListingWithAccurateHaveInformationReturnsNoItems()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            var haveInformation = new List<HaveStatus>();
            foreach (var bookmark in folderContent)
            {
                var have = new HaveStatus(bookmark.Id, bookmark.Hash, bookmark.Progress, bookmark.ProgressTimestamp);
                haveInformation.Add(have);
            }

            var (folderContentWithHave, _) = await client.ListAsync(folder.Id, haveInformation);
            Assert.Empty(folderContentWithHave);
        }

        [Fact, Order(22)]
        public async Task ListingWithDeletedBookmarkInHaveInformationReturnsDeletedId()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            var haveInformation = new List<HaveStatus>();
            foreach (var bookmark in folderContent)
            {
                var have = new HaveStatus(bookmark.Id, bookmark.Hash, bookmark.Progress, bookmark.ProgressTimestamp);
                haveInformation.Add(have);
            }

            // Add 2 fake deleted items
            var fakeHave = new HaveStatus(1UL, "X", 0.5F, DateTime.Now);
            haveInformation.Add(fakeHave);

            var fakeHave2 = new HaveStatus(2UL, "X", 0.5F, DateTime.Now);
            haveInformation.Add(fakeHave2);

            var (folderContentWithHave, deletedIds) = await client.ListAsync(folder.Id, haveInformation);
            Assert.Empty(folderContentWithHave);

            // Check the two things we expected to be deleted were deleted
            Assert.Equal(2, deletedIds.Count);
            Assert.Contains(deletedIds, (id) => id == fakeHave.Id);
            Assert.Contains(deletedIds, (id) => id == fakeHave2.Id);
        }

        [Fact, Order(23)]
        public async Task ListingWithPartiallyOutOfDateHaveReturnsOnlyChangedItems()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            var haveInformation = new List<HaveStatus>();
            foreach (var bookmark in folderContent)
            {
                var have = new HaveStatus(bookmark.Id, bookmark.Hash, bookmark.Progress, bookmark.ProgressTimestamp);
                haveInformation.Add(have);
            }

            // Explicitly update the progress of the second item
            var secondBookmark = folderContent[1];
            var newProgress = secondBookmark.Progress + 0.1F;
            if (newProgress > 1.0F)
            {
                newProgress = 0.1F;
            }

            newProgress = Convert.ToSingle(Math.Round(newProgress, 1));

            _ = await client.UpdateReadProgressAsync(secondBookmark.Id, newProgress, DateTime.Now);
            var (folderContentWithHave, _) = await client.ListAsync(folder.Id, haveInformation);

            // Check the two things we expected to be deleted were deleted
            Assert.Equal(1, folderContentWithHave.Count);
            Assert.Contains(folderContentWithHave, (b) =>
            {
                return (b.Id == secondBookmark.Id) &&
                        (b.Progress == newProgress);
            });
        }

        [Fact, Order(24)]
        public async Task ListingLimitRespected()
        {
            var client = this.SharedState.BookmarksClient;
            var folder = this.SharedState.RecentlyAddedFolder!;

            // Get the current state of bookmarks in that folder
            var (folderContent, _) = await client.ListAsync(folder.Id);
            Assert.Equal(2, folderContent.Count);

            var (folderContentWithHave, _) = await client.ListAsync(folder.Id, limit: 1);
            Assert.Equal(1, folderContentWithHave.Count); // Limited to 1 item, should only get one
        }

        [Fact]
        public async Task RequestingBookmarkTextForInvalidBookmarkThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.GetTextAsync(0));
        }

        [Fact, Order(25)]
        public async Task CanGetArticleText()
        {
            var client = this.SharedState.BookmarksClient;
            var content = await client.GetTextAsync(this.SharedState.RecentlyAddedBookmark!.Id);
            Assert.False(String.IsNullOrWhiteSpace(content)); // Check we have content
        }

        [Fact, Order(26)]
        public async Task Requesting404ingArticleThrowsCorrectError()
        {
            var client = this.SharedState.BookmarksClient;
            await Assert.ThrowsAsync<BookmarkContentsUnavailableException>(() =>
            {
                return client.GetTextAsync(this.SharedState.NotFoundBookmark!.Id);
            });
        }

        [Fact, Order(27)]
        public async Task CanDeletedAddedBookmark()
        {
            var client = this.SharedState.BookmarksClient;

            await client.DeleteAsync(this.SharedState.RecentlyAddedBookmark!.Id);

            var (folderContents, _) = await client.ListAsync(this.SharedState.RecentlyAddedFolder!.Id);
            Assert.Equal(1, folderContents.Count);
            Assert.DoesNotContain(folderContents, (b) => (b.Id == this.SharedState.RecentlyAddedBookmark!.Id));

            this.SharedState.UpdateOrSetRecentBookmark(null);
        }

        [Fact]
        public async Task DeletingWithInvalidBookmarkIdThrows()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Client.DeleteAsync(0UL));
        }

        [Fact]
        public async Task DeleteNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () => await this.Client.DeleteAsync(1UL));
        }
    }
}