using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class LocalOnlyStateTests : IAsyncLifetime
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
                new("https://www.codevoid.net/1"),
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

        private static DatabaseLocalOnlyArticleState GetSampleLocalOnlyState(long articleId)
        {
            return new DatabaseLocalOnlyArticleState()
            {
                ArticleId = articleId,
                AvailableLocally = true,
                FirstImageLocalPath = new($"localimage://local/{articleId}"),
                FirstImageRemoteUri = new($"remoteimage://remote/{articleId}"),
                LocalPath = new($"localfile://local/{articleId}"),
                ExtractedDescription = string.Empty,
                ArticleUnavailable = true,
                IncludeInMRU = false
            };
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
            Assert.Equal(data, result);
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
            var data = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId) with
            { ExtractedDescription = extractedDescription };

            _ = await this.db!.AddLocalOnlyStateForArticleAsync(data);
            var result = (await this.db!.GetLocalOnlyStateByArticleIdAsync(articleId))!;
            Assert.Equal(data, result);
        }

        [Fact]
        public async Task ReadingArticleIncludesLocalStateWhenPresent()
        {
            var articleId = this.sampleArticles.First().Id;
            var extractedDescription = "SampleExtractedDescription";

            var data = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId) with
            { ExtractedDescription = extractedDescription };

            _ = await this.db!.AddLocalOnlyStateForArticleAsync(data);
            var article = (await this.db!.GetArticleByIdAsync(articleId))!;
            Assert.True(article.HasLocalState);
            Assert.Equal(data, article.LocalOnlyState!);
        }

        [Fact]
        public async Task ListingArticlesWithPartialLocalStateReturnsLocalStateCorrectly()
        {
            var articleId = this.sampleArticles.First().Id;
            var articleData = LocalOnlyStateTests.GetSampleLocalOnlyState(articleId);

            _ = await this.db!.AddLocalOnlyStateForArticleAsync(articleData);
            var articles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);

            var articleWithLocalState = (from a in articles
                                         where a.Id == articleId
                                         select a).Single();
            Assert.True(articleWithLocalState!.HasLocalState);
            Assert.Equal(articleData, articleWithLocalState.LocalOnlyState!);

            var articlesWithoutLocalState = (from a in articles
                                             where (a.Id != articleId) && !a.HasLocalState
                                             select a).Count();
            Assert.Equal(articles.Count - 1, articlesWithoutLocalState);
        }

        [Fact]
        public async Task ListingArticlesWhichAllHaveLocalStateCorrectlyReturnsLocalState()
        {
            var addedLocalState = new List<DatabaseLocalOnlyArticleState>();

            foreach (var a in this.sampleArticles)
            {
                var data = LocalOnlyStateTests.GetSampleLocalOnlyState(a.Id);
                _ = await this.db!.AddLocalOnlyStateForArticleAsync(data);

                addedLocalState.Add(data);
            }

            var articles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            var articlesWithLocalState = (from a in articles
                                          where a.HasLocalState
                                          select a);
            Assert.Equal(addedLocalState.Count, articlesWithLocalState.Count());

            foreach (var localState in addedLocalState)
            {
                var matchingArticle = (from ma in articlesWithLocalState
                                       where ma.Id == localState.ArticleId
                                       select ma).Single();
                Assert.NotNull(matchingArticle);
                Assert.True(matchingArticle.HasLocalState);
                Assert.Equal(localState, matchingArticle.LocalOnlyState);
            }
        }

        [Fact]
        public async Task CanRemoveLocalOnlyStateForArticleWhenPresent()
        {
            var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
            _ = await this.db!.AddLocalOnlyStateForArticleAsync(state);

            await this.db!.DeleteLocalOnlyArticleStateAsync(state.ArticleId);

            var result = await this.db!.GetLocalOnlyStateByArticleIdAsync(state.ArticleId);
            Assert.Null(result);
        }

        [Fact]
        public async Task CanRemoveLocalOnlyStateWhenArticleIsPresentButStateIsnt()
        {
            await this.db!.DeleteLocalOnlyArticleStateAsync(this.sampleArticles.First()!.Id);
        }

        [Fact]
        public async Task WhenRemovingStateForArticleThatIsntPresentNoErrorReturned()
        {
            await this.db!.DeleteLocalOnlyArticleStateAsync(999L);
        }

        [Fact]
        public async Task CanUpdateSingleFieldLocalOnlyStateWithExistingState()
        {
            var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
            _ = await this.db!.AddLocalOnlyStateForArticleAsync(state);

            var newState = state with
            {
                ExtractedDescription = "I have been updated"
            };

            var updated = await this.db!.UpdateLocalOnlyArticleStateAsync(newState);
            var readFromDatabase = await this.db!.GetLocalOnlyStateByArticleIdAsync(newState.ArticleId);

            Assert.NotEqual(state, updated);
            Assert.Equal(newState, updated);
            Assert.Equal(newState, readFromDatabase);
        }

        [Fact]
        public async Task CanUpdateAllFieldsFieldLocalOnlyStateWithExistingState()
        {
            var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
            _ = await this.db!.AddLocalOnlyStateForArticleAsync(state);

            var newState = state with
            {
                AvailableLocally = state.AvailableLocally!,
                FirstImageLocalPath = new Uri("imageLocalPathNew://localPathNew"),
                FirstImageRemoteUri = new Uri("imageRemotePathNew://remotePathNew"),
                LocalPath = new Uri("articleLocalPathNew://localPathNew"),
                ExtractedDescription = "Extracted with care",
                ArticleUnavailable = !state.ArticleUnavailable,
                IncludeInMRU = !state.IncludeInMRU
            };

            var updated = await this.db!.UpdateLocalOnlyArticleStateAsync(newState);
            var readFromDatabase = await this.db!.GetLocalOnlyStateByArticleIdAsync(newState.ArticleId);

            Assert.NotEqual(state, updated);
            Assert.Equal(newState, updated);
            Assert.Equal(newState, readFromDatabase);
        }

        [Fact]
        public async Task UpdatingLocalOnlyStateWhenNoLocalStatePresentThrowsException()
        {
            var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
            await Assert.ThrowsAsync<LocalOnlyStateNotFoundException>(async () => await this.db!.UpdateLocalOnlyArticleStateAsync(state));
        }

        [Fact]
        public void UpdatingLocalOnlyStateWithInvalidArticleIdThrowException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.db!.UpdateLocalOnlyArticleStateAsync(new DatabaseLocalOnlyArticleState()));
        }

        [Fact]
        public async Task DeletingArticleAlsoDeletesLocalOnlyState()
        {
            var state = LocalOnlyStateTests.GetSampleLocalOnlyState(this.sampleArticles.First()!.Id);
            _ = await this.db!.AddLocalOnlyStateForArticleAsync(state);

            await this.db!.DeleteArticleAsync(state.ArticleId);

            var result = await this.db!.GetLocalOnlyStateByArticleIdAsync(state.ArticleId);
            Assert.Null(result);
        }
    }
}