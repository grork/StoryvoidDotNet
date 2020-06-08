using System;
using System.Collections.Generic;
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
            IList<object> bookmarks = await this.db!.GetBookmarks(this.UnreadFolder!.LocalId);
            Assert.Empty(bookmarks);
        }

        [Fact]
        public async Task CanAddBookmark()
        {
            DatabaseBookmark bookmark = await this.db!.AddBookmark((
                id: 1,
                title: "Sample Bookmark",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                progress: 0.0F,
                progressTimestamp: DateTime.Now,
                hash: String.Empty,
                liked: false
            ), this.UnreadFolder!.LocalId);

            IList<object> bookmarks = await this.db!.GetBookmarks(this.UnreadFolder.LocalId);
            Assert.Equal(1, bookmarks.Count);
        }
    }
}
