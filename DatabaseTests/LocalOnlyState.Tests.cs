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
        private IList<DatabaseArticle> sampleArticles = new List<DatabaseArticle>();

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();
            this.sampleArticles = await this.PopulateDatabaseWithArticles();
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<IList<DatabaseArticle>> PopulateDatabaseWithArticles()
        {
            var unreadFolder = this.db!.UnreadFolderLocalId;
            var article1 = await this.db!.AddArticleToFolderAsync(new(
                1,
                "Sample Article 1",
                new ("https://www.codevoid.net/1"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            var article2 = await this.db!.AddArticleToFolderAsync(new(
                2,
                "Sample Article 2",
                new("https://www.codevoid.net/2"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            var article3 = await this.db!.AddArticleToFolderAsync(new(
                3,
                "Sample Article 3",
                new("https://www.codevoid.net/2"),
                String.Empty,
                0.0F,
                DateTime.Now,
                String.Empty,
                false
            ), unreadFolder);

            return new List<DatabaseArticle> { article1, article2, article3 };
        }

        [Fact]
        public async Task RequestingLocalStateForMissingArticleReturnsNothing()
        {
            var result = await this.db!.GetLocalOnlyStateByArticleIdAsync(this.sampleArticles.First().Id);
            Assert.Null(result);
        }

        [Fact]
        public async Task CanAddLocalStateForArticle()
        {
            var data = new DatabaseLocalOnlyArticleState()
            {
                ArticleId = this.sampleArticles.First().Id,
            };

            var result = await this.db!.AddLocalOnlyStateForArticleAsync(data);
            Assert.Equal(data.ArticleId, result.ArticleId);
        }

        [Fact]
        public async Task AddingLocalStateForMissingArticleThrowNotFoundException()
        {
            var data = new DatabaseLocalOnlyArticleState()
            {
                ArticleId = 99
            };

            var ex = await Assert.ThrowsAsync<ArticleNotFoundException>(async () => await this.db!.AddLocalOnlyStateForArticleAsync(data));
            Assert.Equal(data.ArticleId, ex.ArticleId);
        }

        [Fact]
        public async Task AddingLocalStateWhenAlreadyPresentThrowsDuplicateException()
        {
            var data = new DatabaseLocalOnlyArticleState()
            {
                ArticleId = this.sampleArticles.First().Id,
            };

            _ = await this.db!.AddLocalOnlyStateForArticleAsync(data);
            var ex = await Assert.ThrowsAsync<LocalOnlyStateExistsException>(async () => await this.db!.AddLocalOnlyStateForArticleAsync(data));
            Assert.Equal(data.ArticleId, ex.ArticleId);
        }

        [Fact]
        public async Task CanReadLocalStateForArticle()
        {
            var articleId = this.sampleArticles.First().Id;
            var extractedDescription = "SampleExtractedDescription";

            var data = new DatabaseLocalOnlyArticleState()
            {
                ArticleId = articleId,
                AvailableLocally = true,
                FirstImageLocalPath = new("localimage://local"),
                FirstImageRemoteUri = new("remoteimage://remote"),
                LocalPath = new("localfile://local"),
                ExtractedDescription = extractedDescription,
                ArticleUnavailable = true,
                IncludeInMRU = false
            };

            _ = await this.db!.AddLocalOnlyStateForArticleAsync(data);
            var result = (await this.db!.GetLocalOnlyStateByArticleIdAsync(articleId))!;
            Assert.Equal(data, result);
        }
    }
}