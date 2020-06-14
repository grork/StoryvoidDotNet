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
    }
}