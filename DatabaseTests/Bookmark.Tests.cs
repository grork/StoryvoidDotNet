using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class BookmarkTests : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private DatabaseFolder? CustomFolder1;
        private DatabaseFolder? CustomFolder2;
        private DatabaseFolder? UnreadFolder;
        private DatabaseFolder? ArchiveFolder;

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();

            // Add sample folders
            var customFolder1 = await this.db!.CreateFolderAsync("Sample1");
            customFolder1 = await this.db!.UpdateFolderAsync(
                customFolder1.LocalId,
                9L,
                customFolder1.Title,
                customFolder1.Position,
                customFolder1.SyncToMobile
            );
            this.CustomFolder1 = customFolder1;

            var customFolder2 = await this.db!.CreateFolderAsync("Sample2");
            customFolder2 = await this.db!.UpdateFolderAsync(
                customFolder2.LocalId,
                10L,
                customFolder2.Title,
                customFolder2.Position,
                customFolder2.SyncToMobile
            );

            this.CustomFolder2 = customFolder2;

            this.UnreadFolder = await this.db!.GetFolderByServiceIdAsync(WellKnownFolderIds.Unread);
            this.ArchiveFolder = await this.db!.GetFolderByServiceIdAsync(WellKnownFolderIds.Archive);
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        private int nextBookmarkId = 0;
        private async Task<DatabaseBookmark> AddRandomBookmarkToFolder(long localFolderId)
        {
            var bookmark = await this.db!.AddBookmark((
                id: nextBookmarkId++,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), localFolderId);

            return bookmark;
        }

        [Fact]
        public async Task CanListBookmarksWhenEmpty()
        {
            var bookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Empty(bookmarks);
        }

        [Fact]
        public async Task CanAddBookmark()
        {
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);
        }

        [Fact]
        public async Task CanGetSingleBookmark()
        {
            var progressTimestamp = DateTime.Now;
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: progressTimestamp,
                hash: "ABC",
                liked: false
            ), this.UnreadFolder!.LocalId);

            var retrievedBookmark = (await this.db!.GetBookmarkById(1L))!;
            Assert.Equal(progressTimestamp, retrievedBookmark.ProgressTimestamp);
            Assert.Equal("Sample Bookmark", retrievedBookmark.Title);
            Assert.Equal(new Uri("https://www.bing.com"), retrievedBookmark.Url);
            Assert.Equal("ABC", retrievedBookmark.Hash);
        }

        [Fact]
        public async Task GettingNonExistantBookmarkReturnsNull()
        {
            var missingBookmark = await this.db!.GetBookmarkById(1);
            Assert.Null(missingBookmark);
        }

        [Fact]
        public async Task CanListBookmarksInUnreadFolder()
        {
            var bookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var bookmarks = await this.db!.GetBookmarks(this.UnreadFolder.LocalId);
            Assert.Equal(1, bookmarks.Count);
            Assert.Contains(bookmarks, (b) => b.Id == 1);

            var bookmarkFromListing = bookmarks.First();
            Assert.Equal(bookmark.ProgressTimestamp, bookmarkFromListing.ProgressTimestamp);
            Assert.Equal(bookmark.Title, bookmarkFromListing.Title);
            Assert.Equal(bookmark.Url, bookmarkFromListing.Url);
            Assert.Equal(bookmark.Hash, bookmarkFromListing.Hash);
        }

        [Fact]
        public async Task CanAddBookmarkToSpecificFolder()
        {
            var bookmark = await this.db!.AddBookmark((
                   id: 1,
                   title: "Sample Bookmark",
                   url: new Uri("https://www.bing.com"),
                   description: String.Empty,
                   progress: 0.0F,
                   progressTimestamp: DateTime.Now,
                   hash: String.Empty,
                   liked: false
               ), this.CustomFolder1!.LocalId);

            var bookmarks = await this.db!.GetBookmarks(this.CustomFolder1.LocalId);
            Assert.Equal(1, bookmarks.Count);
            Assert.Contains(bookmarks, (b) => b.Id == 1);

            var bookmarkFromListing = bookmarks.First();
            Assert.Equal(bookmark.ProgressTimestamp, bookmarkFromListing.ProgressTimestamp);
            Assert.Equal(bookmark.Title, bookmarkFromListing.Title);
            Assert.Equal(bookmark.Url, bookmarkFromListing.Url);
            Assert.Equal(bookmark.Hash, bookmarkFromListing.Hash);
        }

        [Fact]
        public async Task BookmarksAreOnlyReturnedInTheirOwningFolders()
        {
            var customFolderBookmark = await this.db!.AddBookmark((
                  id: 1,
                  title: "Sample Bookmark",
                  url: new Uri("https://www.bing.com"),
                  description: String.Empty,
                  progress: 0.0F,
                  progressTimestamp: DateTime.Now,
                  hash: String.Empty,
                  liked: false
              ), this.CustomFolder1!.LocalId);

            var unreadFolderBookmark = await this.db!.AddBookmark((
                id: 2,
                title: "Sample Bookmark 2",
                url: new Uri("https://www.duckduckgo.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var customFolderBookmarks = await this.db!.GetBookmarks(this.CustomFolder1.LocalId);
            Assert.Equal(1, customFolderBookmarks.Count);
            Assert.Contains(customFolderBookmarks, (b) => b.Id == 1);

            var customBookmarkFromListing = customFolderBookmarks.First();
            Assert.Equal(customFolderBookmark.ProgressTimestamp, customBookmarkFromListing.ProgressTimestamp);
            Assert.Equal(customFolderBookmark.Title, customBookmarkFromListing.Title);
            Assert.Equal(customFolderBookmark.Url, customBookmarkFromListing.Url);
            Assert.Equal(customFolderBookmark.Hash, customBookmarkFromListing.Hash);

            var unreadFolderBookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Equal(1, unreadFolderBookmarks.Count);
            Assert.Contains(unreadFolderBookmarks, (b) => b.Id == 2);

            var unreadBookmarkFromListing = unreadFolderBookmarks.First();
            Assert.Equal(unreadFolderBookmark.ProgressTimestamp, unreadBookmarkFromListing.ProgressTimestamp);
            Assert.Equal(unreadFolderBookmark.Title, unreadBookmarkFromListing.Title);
            Assert.Equal(unreadFolderBookmark.Url, unreadBookmarkFromListing.Url);
            Assert.Equal(unreadFolderBookmark.Hash, unreadBookmarkFromListing.Hash);
        }

        [Fact]
        public async Task ListingLikedBookmarksWithNoLikedBookmarksReturnsEmptyList()
        {
            var likedBookmarks = await this.db!.GetLikedBookmarks();
            Assert.Empty(likedBookmarks);
        }

        [Fact]
        public async Task CanLikeBookmarkThatIsUnliked()
        {
            var unlikedBookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var likedBookmark = await this.db!.LikeBookmark(unlikedBookmark.Id);
            Assert.Equal(unlikedBookmark.Id, likedBookmark.Id);
            Assert.True(likedBookmark.Liked);
        }

        [Fact]
        public async Task CanListOnlyLikedBookmarks()
        {
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: true
            ), this.UnreadFolder!.LocalId);

            var likedBookmarks = await this.db!.GetLikedBookmarks();
            Assert.Equal(1, likedBookmarks.Count);
            Assert.Contains(likedBookmarks, (b) => (b.Id == 1) && b.Liked);
        }

        [Fact]
        public async Task ListingLikedBookmarksReturnsResultsAcrossFolders()
        {
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: true
            ), this.UnreadFolder!.LocalId);

            _ = await this.db!.AddBookmark((
                id: 2,
                title: "Sample Bookmark 2",
                url: new Uri("https://www.duckduckgo.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: true
            ), this.CustomFolder1!.LocalId);

            var likedBookmarks = await this.db!.GetLikedBookmarks();
            Assert.Equal(2, likedBookmarks.Count);
            Assert.Contains(likedBookmarks, (b) => (b.Id == 1) && b.Liked);
            Assert.Contains(likedBookmarks, (b) => (b.Id == 2) && b.Liked);
        }

        [Fact]
        public async Task CanUnlikeBookmarkThatIsLiked()
        {
            var likedBookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: true
            ), this.UnreadFolder!.LocalId);

            var unlikedBookmark = await this.db!.UnlikeBookmark(likedBookmark.Id);
            Assert.Equal(likedBookmark.Id, unlikedBookmark.Id);
            Assert.False(unlikedBookmark.Liked);
        }

        [Fact]
        public async Task LikingMissingBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                _ = await this.db!.LikeBookmark(1);
            });
        }

        [Fact]
        public async Task UnlikingMissingBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                _ = await this.db!.UnlikeBookmark(1);
            });
        }

        [Fact]
        public async Task LikingBookmarkThatIsLikedSucceeds()
        {
            var likedBookmarkOriginal = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: true
            ), this.UnreadFolder!.LocalId);

            var likedBookmark = await this.db!.LikeBookmark(likedBookmarkOriginal.Id);
            Assert.Equal(likedBookmarkOriginal.Id, likedBookmark.Id);
            Assert.True(likedBookmark.Liked);
        }

        [Fact]
        public async Task UnlikingBookmarkThatIsNotLikedSucceeds()
        {
            var unlikedBookmarkOriginal = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var unlikedBookmark = await this.db!.UnlikeBookmark(unlikedBookmarkOriginal.Id);
            Assert.Equal(unlikedBookmarkOriginal.Id, unlikedBookmark.Id);
            Assert.False(unlikedBookmark.Liked);
        }

        [Fact]
        public async Task CanUpdateBookmarkProgressWithTimeStamp()
        {
            var bookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            DatabaseBookmark updatedBookmark = await this.db!.UpdateProgressForBookmark(progress, progressTimestamp, bookmark.Id);
            Assert.Equal(bookmark.Id, updatedBookmark.Id);
            Assert.Equal(progressTimestamp, updatedBookmark.ProgressTimestamp);
            Assert.Equal(progress, updatedBookmark.Progress);
        }

        [Fact]
        public async Task ProgressUpdateChangesReflectedInListCall()
        {
            var bookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            var beforeUpdate = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Equal(1, beforeUpdate.Count);
            Assert.Contains(beforeUpdate, (b) =>
                (b.Id == 1) && b.Progress == bookmark.Progress && b.ProgressTimestamp == bookmark.ProgressTimestamp);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            bookmark = await this.db!.UpdateProgressForBookmark(progress, progressTimestamp, bookmark.Id);
            var afterUpdate = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Equal(1, afterUpdate.Count);
            Assert.Contains(afterUpdate, (b) =>
                (b.Id == 1) && b.Progress == progress && b.ProgressTimestamp == progressTimestamp);
        }

        [Fact]
        public async Task UpdatingProgressOfNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                await this.db!.UpdateProgressForBookmark(0.4F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressOutsideSupportedRangeThrows()
        {
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmark(-0.01F, DateTime.Now, 1);
            });

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmark(1.01F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressWithTimeStampOutsideUnixEpochThrows()
        {
            _ = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmark(0.5F, new DateTime(1969, 12, 31, 23, 59, 59), 1);
            });
        }

        [Fact]
        public async Task CanMoveBookmarkFromUnreadToCustomFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.UnreadFolder!.LocalId);
            await this.db!.MoveBookmarkToFolder(bookmark.Id, this.CustomFolder1!.LocalId);

            // Check it's in the destination
            var customBookmarks = await this.db!.GetBookmarks(this.CustomFolder1.LocalId);
            Assert.Equal(1, customBookmarks.Count);
            Assert.Contains(customBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var unreadBookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromUnreadToArchiveFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.UnreadFolder!.LocalId);
            await this.db!.MoveBookmarkToFolder(bookmark.Id, this.ArchiveFolder!.LocalId);

            // Check it's in the destination
            var archiveBookmarks = await this.db!.GetBookmarks(this.ArchiveFolder!.LocalId);
            Assert.Equal(1, archiveBookmarks.Count);
            Assert.Contains(archiveBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var unreadBookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromCustomFolderToUnread()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.MoveBookmarkToFolder(bookmark.Id, this.UnreadFolder!.LocalId);

            // Check it's in the destination
            var unreadBookmarks = await this.db!.GetBookmarks(this.UnreadFolder.LocalId);
            Assert.Equal(1, unreadBookmarks.Count);
            Assert.Contains(unreadBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var customBookmarks = await this.db!.GetBookmarks(this.CustomFolder1!.LocalId);
            Assert.Empty(customBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromArchiveToUnread()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.ArchiveFolder!.LocalId);
            await this.db!.MoveBookmarkToFolder(bookmark.Id, this.UnreadFolder!.LocalId);

            // Check it's in the destination
            var unreadBookmarks = await this.db!.GetBookmarks(this.UnreadFolder.LocalId);
            Assert.Equal(1, unreadBookmarks.Count);
            Assert.Contains(unreadBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var archiveBookmarks = await this.db!.GetBookmarks(this.ArchiveFolder!.LocalId);
            Assert.Empty(archiveBookmarks);
        }

        [Fact]
        public async Task MovingBookmarkFromUnreadToNonExistantFolderThrows()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.UnreadFolder!.LocalId);
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveBookmarkToFolder(bookmark.Id, 999));
        }

        [Fact]
        public async Task MovingNonExistantBookmarkToCustomFolder()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(() => this.db!.MoveBookmarkToFolder(999, this.CustomFolder1!.LocalId));
        }

        [Fact]
        public async Task MovingNonExistantBookmarkToNonExistantFolder()
        {
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveBookmarkToFolder(999, 888));
        }

        [Fact]
        public async Task DeletingFolderContainingBookmarksRemovesFolder()
        {
            _ = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            _ = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);

            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            var folders = await this.db!.GetFoldersAsync();
            Assert.DoesNotContain(folders, (f) => f.LocalId == this.CustomFolder1!.LocalId);
        }

        [Fact]
        public async Task CanDeleteBookmarkInUnreadFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.DeleteBookmark(bookmark.Id);

            var unreadBookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanDeleteBookmarkInCustomFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteBookmark(bookmark.Id);

            var customBookmarks = await this.db!.GetBookmarks(this.CustomFolder1.LocalId);
            Assert.Empty(customBookmarks);
        }

        [Fact]
        public async Task CanDeleteNonExistantBookmark()
        {
            await this.db!.DeleteBookmark(999);
        }

        [Fact]
        public async Task CanDeleteOrphanedBookmark()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            await this.db!.DeleteBookmark(bookmark.Id);
        }

        public async Task CanGetOrphanedBookmark()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);

            var orphaned = await this.db!.GetBookmarkById(bookmark.Id);
            Assert.NotNull(orphaned);
            Assert.Equal(bookmark.Id, orphaned!.Id);
        }
    }
}