using System;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class BookmarkTests : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private DatabaseFolder? CustomFolder1;
        private DatabaseFolder? CustomFolder2;

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
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        private int nextBookmarkId = 0;
        private (int id, string title, Uri url, string description, float progress, DateTime progressTimestamp, string hash, bool liked) GetRandomBookmark()
        {
            return (
                id: nextBookmarkId++,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: "ABC",
                liked: false
            );
        }

        private async Task<DatabaseBookmark> AddRandomBookmarkToFolder(long localFolderId)
        {
            var bookmark = await this.db!.AddBookmarkAsync(
                this.GetRandomBookmark(),
                localFolderId
            );

            return bookmark;
        }

        [Fact]
        public async Task CanListBookmarksWhenEmpty()
        {
            var bookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(bookmarks);
        }

        [Fact]
        public async Task CanAddBookmark()
        {
            var b = this.GetRandomBookmark();
            _ = await this.db!.AddBookmarkAsync(b, this.db!.UnreadFolderLocalId);
        }

        [Fact]
        public async Task CanGetSingleBookmark()
        {
            var b = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);

            var retrievedBookmark = (await this.db!.GetBookmarkByIdAsync(b.Id))!;
            Assert.Equal(b.ProgressTimestamp, retrievedBookmark.ProgressTimestamp);
            Assert.Equal(b.Title, retrievedBookmark.Title);
            Assert.Equal(b.Url, retrievedBookmark.Url);
            Assert.Equal(b.Hash, retrievedBookmark.Hash);
        }

        [Fact]
        public async Task GettingNonExistantBookmarkReturnsNull()
        {
            var missingBookmark = await this.db!.GetBookmarkByIdAsync(1);
            Assert.Null(missingBookmark);
        }

        [Fact]
        public async Task CanListBookmarksInUnreadFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);

            var bookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, bookmarks.Count);
            Assert.Contains(bookmarks, (b) => b.Id == bookmark.Id);

            var bookmarkFromListing = bookmarks.First();
            Assert.Equal(bookmark.ProgressTimestamp, bookmarkFromListing.ProgressTimestamp);
            Assert.Equal(bookmark.Title, bookmarkFromListing.Title);
            Assert.Equal(bookmark.Url, bookmarkFromListing.Url);
            Assert.Equal(bookmark.Hash, bookmarkFromListing.Hash);
        }

        [Fact]
        public async Task CanAddBookmarkToSpecificFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);

            var bookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, bookmarks.Count);
            Assert.Contains(bookmarks, (b) => b.Id == bookmark.Id);

            var bookmarkFromListing = bookmarks.First();
            Assert.Equal(bookmark.ProgressTimestamp, bookmarkFromListing.ProgressTimestamp);
            Assert.Equal(bookmark.Title, bookmarkFromListing.Title);
            Assert.Equal(bookmark.Url, bookmarkFromListing.Url);
            Assert.Equal(bookmark.Hash, bookmarkFromListing.Hash);
        }

        [Fact]
        public async Task BookmarksAreOnlyReturnedInTheirOwningFolders()
        {
            var customFolderBookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            var unreadFolderBookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);

            var customFolderBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, customFolderBookmarks.Count);
            Assert.Contains(customFolderBookmarks, (b) => b.Id == customFolderBookmark.Id);

            var customBookmarkFromListing = customFolderBookmarks.First();
            Assert.Equal(customFolderBookmark.ProgressTimestamp, customBookmarkFromListing.ProgressTimestamp);
            Assert.Equal(customFolderBookmark.Title, customBookmarkFromListing.Title);
            Assert.Equal(customFolderBookmark.Url, customBookmarkFromListing.Url);
            Assert.Equal(customFolderBookmark.Hash, customBookmarkFromListing.Hash);

            var unreadFolderBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadFolderBookmarks.Count);
            Assert.Contains(unreadFolderBookmarks, (b) => b.Id == unreadFolderBookmark.Id);

            var unreadBookmarkFromListing = unreadFolderBookmarks.First();
            Assert.Equal(unreadFolderBookmark.ProgressTimestamp, unreadBookmarkFromListing.ProgressTimestamp);
            Assert.Equal(unreadFolderBookmark.Title, unreadBookmarkFromListing.Title);
            Assert.Equal(unreadFolderBookmark.Url, unreadBookmarkFromListing.Url);
            Assert.Equal(unreadFolderBookmark.Hash, unreadBookmarkFromListing.Hash);
        }

        [Fact]
        public async Task ListingLikedBookmarksWithNoLikedBookmarksReturnsEmptyList()
        {
            var likedBookmarks = await this.db!.GetLikedBookmarksAsync();
            Assert.Empty(likedBookmarks);
        }

        [Fact]
        public async Task CanLikeBookmarkThatIsUnliked()
        {
            var unlikedBookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            var likedBookmark = await this.db!.LikeBookmarkAsync(unlikedBookmark.Id);
            Assert.Equal(unlikedBookmark.Id, likedBookmark.Id);
            Assert.True(likedBookmark.Liked);
        }

        [Fact]
        public async Task CanListOnlyLikedBookmarks()
        {
            var bookmark = this.GetRandomBookmark();
            bookmark.liked = true;

            _ = await this.db!.AddBookmarkAsync(
                bookmark,
                this.db!.UnreadFolderLocalId
            );

            var likedBookmarks = await this.db!.GetLikedBookmarksAsync();
            Assert.Equal(1, likedBookmarks.Count);
            Assert.Contains(likedBookmarks, (b) => (b.Id == bookmark.id) && b.Liked);
        }

        [Fact]
        public async Task ListingLikedBookmarksReturnsResultsAcrossFolders()
        {
            var bookmark1 = this.GetRandomBookmark();
            bookmark1.liked = true;
            _ = await this.db!.AddBookmarkAsync(bookmark1, this.db!.UnreadFolderLocalId);

            var bookmark2 = this.GetRandomBookmark();
            bookmark2.liked = true;
            _ = await this.db!.AddBookmarkAsync(bookmark2, this.CustomFolder1!.LocalId);

            var likedBookmarks = await this.db!.GetLikedBookmarksAsync();
            Assert.Equal(2, likedBookmarks.Count);
            Assert.Contains(likedBookmarks, (b) => (b.Id == bookmark1.id) && b.Liked);
            Assert.Contains(likedBookmarks, (b) => (b.Id == bookmark2.id) && b.Liked);
        }

        [Fact]
        public async Task CanUnlikeBookmarkThatIsLiked()
        {
            var b = this.GetRandomBookmark();
            b.liked = true;
            var likedBookmark = await this.db!.AddBookmarkAsync(b, this.db!.UnreadFolderLocalId);

            var unlikedBookmark = await this.db!.UnlikeBookmarkAsync(likedBookmark.Id);
            Assert.Equal(likedBookmark.Id, unlikedBookmark.Id);
            Assert.False(unlikedBookmark.Liked);
        }

        [Fact]
        public async Task LikingMissingBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                _ = await this.db!.LikeBookmarkAsync(1);
            });
        }

        [Fact]
        public async Task UnlikingMissingBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                _ = await this.db!.UnlikeBookmarkAsync(1);
            });
        }

        [Fact]
        public async Task LikingBookmarkThatIsLikedSucceeds()
        {
            var likedBookmarkOriginal = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            var likedBookmark = await this.db!.LikeBookmarkAsync(likedBookmarkOriginal.Id);

            Assert.Equal(likedBookmarkOriginal.Id, likedBookmark.Id);
            Assert.True(likedBookmark.Liked);
        }

        [Fact]
        public async Task UnlikingBookmarkThatIsNotLikedSucceeds()
        {
            var unlikedBookmarkOriginal = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            var unlikedBookmark = await this.db!.UnlikeBookmarkAsync(unlikedBookmarkOriginal.Id);

            Assert.Equal(unlikedBookmarkOriginal.Id, unlikedBookmark.Id);
            Assert.False(unlikedBookmark.Liked);
        }

        [Fact]
        public async Task CanUpdateBookmarkProgressWithTimeStamp()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            DatabaseBookmark updatedBookmark = await this.db!.UpdateProgressForBookmarkAsync(progress, progressTimestamp, bookmark.Id);
            Assert.Equal(bookmark.Id, updatedBookmark.Id);
            Assert.Equal(progressTimestamp, updatedBookmark.ProgressTimestamp);
            Assert.Equal(progress, updatedBookmark.Progress);
        }

        [Fact]
        public async Task ProgressUpdateChangesReflectedInListCall()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);

            var beforeUpdate = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, beforeUpdate.Count);
            Assert.Contains(beforeUpdate, (b) =>
                (b.Id == bookmark.Id) && b.Progress == bookmark.Progress && b.ProgressTimestamp == bookmark.ProgressTimestamp);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            bookmark = await this.db!.UpdateProgressForBookmarkAsync(progress, progressTimestamp, bookmark.Id);
            var afterUpdate = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, afterUpdate.Count);
            Assert.Contains(afterUpdate, (b) =>
                (b.Id == bookmark.Id) && b.Progress == progress && b.ProgressTimestamp == progressTimestamp);
        }

        [Fact]
        public async Task UpdatingProgressOfNonExistantBookmarkThrows()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(async () =>
            {
                await this.db!.UpdateProgressForBookmarkAsync(0.4F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressOutsideSupportedRangeThrows()
        {
            _ = await this.db!.AddBookmarkAsync(
                this.GetRandomBookmark(),
                this.db!.UnreadFolderLocalId
            );

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmarkAsync(-0.01F, DateTime.Now, 1);
            });

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmarkAsync(1.01F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressWithTimeStampOutsideUnixEpochThrows()
        {
            _ = await this.db!.AddBookmarkAsync(
                this.GetRandomBookmark(),
                this.db!.UnreadFolderLocalId
            );

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateProgressForBookmarkAsync(0.5F, new DateTime(1969, 12, 31, 23, 59, 59), 1);
            });
        }

        [Fact]
        public async Task CanMoveBookmarkFromUnreadToCustomFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.MoveBookmarkToFolderAsync(bookmark.Id, this.CustomFolder1!.LocalId);

            // Check it's in the destination
            var customBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, customBookmarks.Count);
            Assert.Contains(customBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var unreadBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromUnreadToArchiveFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.MoveBookmarkToFolderAsync(bookmark.Id, this.db!.ArchiveFolderLocalId);

            // Check it's in the destination
            var archiveBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.ArchiveFolderLocalId);
            Assert.Equal(1, archiveBookmarks.Count);
            Assert.Contains(archiveBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var unreadBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromCustomFolderToUnread()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.MoveBookmarkToFolderAsync(bookmark.Id, this.db!.UnreadFolderLocalId);

            // Check it's in the destination
            var unreadBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadBookmarks.Count);
            Assert.Contains(unreadBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var customBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1!.LocalId);
            Assert.Empty(customBookmarks);
        }

        [Fact]
        public async Task CanMoveBookmarkFromArchiveToUnread()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.ArchiveFolderLocalId);
            await this.db!.MoveBookmarkToFolderAsync(bookmark.Id, this.db!.UnreadFolderLocalId);

            // Check it's in the destination
            var unreadBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadBookmarks.Count);
            Assert.Contains(unreadBookmarks, (b) => b.Id == bookmark.Id);

            // Check it's not present in unread
            var archiveBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.ArchiveFolderLocalId);
            Assert.Empty(archiveBookmarks);
        }

        [Fact]
        public async Task MovingBookmarkFromUnreadToNonExistantFolderThrows()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveBookmarkToFolderAsync(bookmark.Id, 999));
        }

        [Fact]
        public async Task MovingBookmarkToAFolderItIsAlreadyInSucceeds()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.MoveBookmarkToFolderAsync(bookmark.Id, this.CustomFolder1!.LocalId);

            var customBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1!.LocalId);
            Assert.Equal(1, customBookmarks.Count);
            Assert.Contains(customBookmarks, (f) => f.Id == bookmark.Id);
        }

        [Fact]
        public async Task MovingNonExistantBookmarkToCustomFolder()
        {
            await Assert.ThrowsAsync<BookmarkNotFoundException>(() => this.db!.MoveBookmarkToFolderAsync(999, this.CustomFolder1!.LocalId));
        }

        [Fact]
        public async Task MovingNonExistantBookmarkToNonExistantFolder()
        {
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveBookmarkToFolderAsync(999, 888));
        }

        [Fact]
        public async Task DeletingFolderContainingBookmarksRemovesFolder()
        {
            _ = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            _ = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);

            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            var folders = await this.db!.GetAllFoldersAsync();
            Assert.DoesNotContain(folders, (f) => f.LocalId == this.CustomFolder1!.LocalId);
        }

        [Fact]
        public async Task CanDeleteBookmarkInUnreadFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.DeleteBookmarkAsync(bookmark.Id);

            var unreadBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadBookmarks);
        }

        [Fact]
        public async Task CanDeleteBookmarkInCustomFolder()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteBookmarkAsync(bookmark.Id);

            var customBookmarks = await this.db!.GetBookmarksForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Empty(customBookmarks);
        }

        [Fact]
        public async Task CanDeleteNonExistantBookmark()
        {
            await this.db!.DeleteBookmarkAsync(999);
        }

        [Fact]
        public async Task CanDeleteOrphanedBookmark()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            await this.db!.DeleteBookmarkAsync(bookmark.Id);
        }

        public async Task CanGetOrphanedBookmark()
        {
            var bookmark = await this.AddRandomBookmarkToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);

            var orphaned = await this.db!.GetBookmarkByIdAsync(bookmark.Id);
            Assert.NotNull(orphaned);
            Assert.Equal(bookmark.Id, orphaned!.Id);
        }
    }
}