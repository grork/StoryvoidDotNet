using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class LocalOnlyState : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private IList<DatabaseBookmark> sampleBookmarks = new List<DatabaseBookmark>();

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();
            this.sampleBookmarks = await this.PopulateDatabaseWithBookmarks();
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<IList<DatabaseBookmark>> PopulateDatabaseWithBookmarks()
        {
            var unreadFolder = this.db!.UnreadFolderLocalId;
            var bookmark1 = await this.db!.AddBookmarkToFolderAsync(new(
                1,
                "Sample Bookmark 1",
                new ("https://www.codevoid.net/1"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            var bookmark2 = await this.db!.AddBookmarkToFolderAsync(new(
                2,
                "Sample Bookmark 2",
                new("https://www.codevoid.net/2"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            var bookmark3 = await this.db!.AddBookmarkToFolderAsync(new(
                3,
                "Sample Bookmark 3",
                new("https://www.codevoid.net/2"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            return new List<DatabaseBookmark> { bookmark1, bookmark2, bookmark3 };
        }

        [Fact]
        public async Task RequestingLocalStateForMissingBookmarkReturnsNothing()
        {
            var result = await this.db!.GetLocalOnlyStateByBookmarkIdAsync(this.sampleBookmarks.First().Id);
            Assert.Null(result);
        }

        [Fact]
        public async Task CanAddLocalStateForBookmark()
        {
            var data = new DatabaseLocalOnlyBookmarkState()
            {
                BookmarkId = this.sampleBookmarks.First().Id,
            };

            var result = await this.db!.AddLocalOnlyStateForBookmarkAsync(data);
            Assert.Equal(data.BookmarkId, result.BookmarkId);
        }

        [Fact]
        public async Task CanReadLocalStateForBookmark()
        {
            var bookmarkId = this.sampleBookmarks.First().Id;
            var extractedDescription = "SampleExtractedDescription";

            var data = new DatabaseLocalOnlyBookmarkState()
            {
                BookmarkId = bookmarkId,
                AvailableLocally = true,
                FirstImageLocalPath = new("localimage://local"),
                FirstImageRemoteUri = new("remoteimage://remote"),
                LocalPath = new("localfile://local"),
                ExtractedDescription = extractedDescription,
                ArticleUnavailable = true,
                IncludeInMRU = false
            };

            _ = await this.db!.AddLocalOnlyStateForBookmarkAsync(data);
            var result = (await this.db!.GetLocalOnlyStateByBookmarkIdAsync(bookmarkId))!;
            Assert.Equal(data, result);
        }
    }
}