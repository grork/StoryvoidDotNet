using System;
using System.Linq;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class ArticleTests : IAsyncLifetime
    {
        private IArticleDatabase? db;
        private DatabaseFolder? CustomFolder1;
        private DatabaseFolder? CustomFolder2;

        public async Task InitializeAsync()
        {
            this.db = await TestUtilities.GetDatabase();

            // Add sample folders
            this.CustomFolder1 = await this.db.AddKnownFolderAsync(title: "Sample1",
                                                                  serviceId: 9L,
                                                                  position: 1,
                                                                  shouldSync: true);

            this.CustomFolder2 = await this.db.AddKnownFolderAsync(title: "Sample2",
                                                                   serviceId: 10L,
                                                                   position: 1,
                                                                   shouldSync: true);
        }

        public Task DisposeAsync()
        {
            this.db?.Dispose();
            return Task.CompletedTask;
        }

        private int nextArticleId = 0;
        private ArticleRecordInformation GetRandomArticle()
        {
            return new(
                id: nextArticleId++,
                title: "Sample Article",
                url: new Uri("https://www.bing.com"),
                description: String.Empty,
                readProgress: 0.0F,
                readProgressTimestamp: DateTime.Now,
                hash: "ABC",
                liked: false
            );
        }

        private async Task<DatabaseArticle> AddRandomArticleToFolder(long localFolderId)
        {
            var article = await this.db!.AddArticleToFolderAsync(
                this.GetRandomArticle(),
                localFolderId
            );

            return article;
        }

        [Fact]
        public async Task CanListArticlesWhenEmpty()
        {
            var articles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(articles);
        }

        [Fact]
        public async Task CanAddArticles()
        {
            var a = this.GetRandomArticle();
            var result = await this.db!.AddArticleToFolderAsync(a, this.db!.UnreadFolderLocalId);

            // Ensure the article we are handed back on completion is the
            // same (for supplied fields) as that which is returned
            Assert.Equal(a.readProgressTimestamp, result.ReadProgressTimestamp);
            Assert.Equal(a.title, result.Title);
            Assert.Equal(a.url, result.Url);
            Assert.Equal(a.hash, result.Hash);

            // Don't expect local state since we have a fresh set of articles
            Assert.False(result.HasLocalState);
        }

        [Fact]
        public async Task CanGetSingleArticle()
        {
            var a = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            var retrievedArticle = (await this.db!.GetArticleByIdAsync(a.Id))!;
            Assert.Equal(a.ReadProgressTimestamp, retrievedArticle.ReadProgressTimestamp);
            Assert.Equal(a.Title, retrievedArticle.Title);
            Assert.Equal(a.Url, retrievedArticle.Url);
            Assert.Equal(a.Hash, retrievedArticle.Hash);

            // Don't expect local state since we have a fresh set of articles
            Assert.False(retrievedArticle.HasLocalState);
        }

        [Fact]
        public async Task GettingNonExistantArticleReturnsNull()
        {
            var missingArticle = await this.db!.GetArticleByIdAsync(1);
            Assert.Null(missingArticle);
        }

        [Fact]
        public async Task CanListArticlesInUnreadFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            var articles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, articles.Count);
            Assert.Contains(articles, (b) => b.Id == article.Id);

            var articleFromListing = articles.First();
            Assert.Equal(article.ReadProgressTimestamp, articleFromListing.ReadProgressTimestamp);
            Assert.Equal(article.Title, articleFromListing.Title);
            Assert.Equal(article.Url, articleFromListing.Url);
            Assert.Equal(article.Hash, articleFromListing.Hash);
            Assert.False(articleFromListing.HasLocalState);
        }

        [Fact]
        public async Task CanAddArticleToSpecificFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);

            var articles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, articles.Count);
            Assert.Contains(articles, (b) => b.Id == article.Id);

            var articleFromlisting = articles.First();
            Assert.Equal(article.ReadProgressTimestamp, articleFromlisting.ReadProgressTimestamp);
            Assert.Equal(article.Title, articleFromlisting.Title);
            Assert.Equal(article.Url, articleFromlisting.Url);
            Assert.Equal(article.Hash, articleFromlisting.Hash);
            Assert.False(articleFromlisting.HasLocalState);
        }

        [Fact]
        public async Task AddingArticleToNonExistantFolderFails()
        {
            var article = this.GetRandomArticle();
            await Assert.ThrowsAsync<FolderNotFoundException>(async () =>
            {
                _ = await this.db!.AddArticleToFolderAsync(article, 999L);
            });
        }

        [Fact]
        public async Task ArticlesAreOnlyReturnedInTheirOwningFolders()
        {
            var customFolderArticle = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            var unreadFolderArticle = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            var customFolderArticles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, customFolderArticles.Count);
            Assert.Contains(customFolderArticles, (b) => b.Id == customFolderArticle.Id);

            var customArticleFromListing = customFolderArticles.First();
            Assert.Equal(customFolderArticle.ReadProgressTimestamp, customArticleFromListing.ReadProgressTimestamp);
            Assert.Equal(customFolderArticle.Title, customArticleFromListing.Title);
            Assert.Equal(customFolderArticle.Url, customArticleFromListing.Url);
            Assert.Equal(customFolderArticle.Hash, customArticleFromListing.Hash);
            Assert.False(customArticleFromListing.HasLocalState);

            var unreadFolderArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadFolderArticles.Count);
            Assert.Contains(unreadFolderArticles, (b) => b.Id == unreadFolderArticle.Id);

            var unreadArticleFromListing = unreadFolderArticles.First();
            Assert.Equal(unreadFolderArticle.ReadProgressTimestamp, unreadArticleFromListing.ReadProgressTimestamp);
            Assert.Equal(unreadFolderArticle.Title, unreadArticleFromListing.Title);
            Assert.Equal(unreadFolderArticle.Url, unreadArticleFromListing.Url);
            Assert.Equal(unreadFolderArticle.Hash, unreadArticleFromListing.Hash);
            Assert.False(unreadArticleFromListing.HasLocalState);
        }

        [Fact]
        public async Task ListingLikedArticlesWithNoLikedArticlesReturnsEmptyList()
        {
            var likedArticles = await this.db!.ListLikedArticleAsync();
            Assert.Empty(likedArticles);
        }

        [Fact]
        public async Task CanLikeArticleThatIsUnliked()
        {
            var unlikedArticle = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            var likedArticle = await this.db!.LikeArticleAsync(unlikedArticle.Id);
            Assert.Equal(unlikedArticle.Id, likedArticle.Id);
            Assert.True(likedArticle.Liked);
        }

        [Fact]
        public async Task CanListOnlyLikedArticle()
        {
            var article = this.GetRandomArticle() with { liked = true };

            _ = await this.db!.AddArticleToFolderAsync(
                article,
                this.db!.UnreadFolderLocalId
            );

            var likedArticles = await this.db!.ListLikedArticleAsync();
            Assert.Equal(1, likedArticles.Count);
            Assert.Contains(likedArticles, (a) => (a.Id == article.id) && a.Liked);
        }

        [Fact]
        public async Task ListingLikedArticlesReturnsResultsAcrossFolders()
        {
            var article1 = this.GetRandomArticle() with { liked = true };
            _ = await this.db!.AddArticleToFolderAsync(article1, this.db!.UnreadFolderLocalId);

            var article2 = this.GetRandomArticle() with { liked = true };
            _ = await this.db!.AddArticleToFolderAsync(article2, this.CustomFolder1!.LocalId);

            var likedArticles = await this.db!.ListLikedArticleAsync();
            Assert.Equal(2, likedArticles.Count);
            Assert.Contains(likedArticles, (a) => (a.Id == article1.id) && a.Liked);
            Assert.Contains(likedArticles, (a) => (a.Id == article2.id) && a.Liked);
        }

        [Fact]
        public async Task CanUnlikeArticleThatIsLiked()
        {
            var a = this.GetRandomArticle() with { liked = true };
            var likedArticle = await this.db!.AddArticleToFolderAsync(a, this.db!.UnreadFolderLocalId);

            var unlikedArticle = await this.db!.UnlikeArticleAsync(likedArticle.Id);
            Assert.Equal(likedArticle.Id, unlikedArticle.Id);
            Assert.False(unlikedArticle.Liked);
        }

        [Fact]
        public async Task LikingMissingArticleThrows()
        {
            await Assert.ThrowsAsync<ArticleNotFoundException>(async () =>
            {
                _ = await this.db!.LikeArticleAsync(1);
            });
        }

        [Fact]
        public async Task UnlikingMissingArticleThrows()
        {
            await Assert.ThrowsAsync<ArticleNotFoundException>(async () =>
            {
                _ = await this.db!.UnlikeArticleAsync(1);
            });
        }

        [Fact]
        public async Task LikingArticleThatIsLikedSucceeds()
        {
            var likedArticleOriginal = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            var likedArticle = await this.db!.LikeArticleAsync(likedArticleOriginal.Id);

            Assert.Equal(likedArticleOriginal.Id, likedArticle.Id);
            Assert.True(likedArticle.Liked);
        }

        [Fact]
        public async Task UnlikingArticleThatIsNotLikedSucceeds()
        {
            var unlikedArticleOriginal = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            var unlikedArticle = await this.db!.UnlikeArticleAsync(unlikedArticleOriginal.Id);

            Assert.Equal(unlikedArticleOriginal.Id, unlikedArticle.Id);
            Assert.False(unlikedArticle.Liked);
        }

        [Fact]
        public async Task CanUpdateArticleProgressWithTimeStamp()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            DatabaseArticle updatedArticle = await this.db!.UpdateReadProgressForArticleAsync(progress, progressTimestamp, article.Id);
            Assert.Equal(article.Id, updatedArticle.Id);
            Assert.Equal(progressTimestamp, updatedArticle.ReadProgressTimestamp);
            Assert.Equal(progress, updatedArticle.ReadProgress);
            Assert.NotEqual(article.Hash, updatedArticle.Hash);
        }

        [Fact]
        public async Task ProgressUpdateChangesReflectedInListCall()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            var beforeUpdate = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, beforeUpdate.Count);
            Assert.Contains(beforeUpdate, (a) =>
                (a.Id == article.Id) && a.ReadProgress == article.ReadProgress && a.ReadProgressTimestamp == article.ReadProgressTimestamp);

            var progressTimestamp = DateTime.Now.AddMinutes(5);
            var progress = 0.3F;
            article = await this.db!.UpdateReadProgressForArticleAsync(progress, progressTimestamp, article.Id);
            var afterUpdate = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, afterUpdate.Count);
            Assert.Contains(afterUpdate, (a) =>
                (a.Id == article.Id) && a.ReadProgress == progress && a.ReadProgressTimestamp == progressTimestamp);
        }

        [Fact]
        public async Task UpdatingProgressOfNonExistantArticleThrows()
        {
            await Assert.ThrowsAsync<ArticleNotFoundException>(async () =>
            {
                await this.db!.UpdateReadProgressForArticleAsync(0.4F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressOutsideSupportedRangeThrows()
        {
            _ = await this.db!.AddArticleToFolderAsync(
                this.GetRandomArticle(),
                this.db!.UnreadFolderLocalId
            );

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateReadProgressForArticleAsync(-0.01F, DateTime.Now, 1);
            });

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateReadProgressForArticleAsync(1.01F, DateTime.Now, 1);
            });
        }

        [Fact]
        public async Task UpdatingProgressWithTimeStampOutsideUnixEpochThrows()
        {
            _ = await this.db!.AddArticleToFolderAsync(
                this.GetRandomArticle(),
                this.db!.UnreadFolderLocalId
            );

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await this.db!.UpdateReadProgressForArticleAsync(0.5F, new DateTime(1969, 12, 31, 23, 59, 59), 1);
            });
        }

        [Fact]
        public async Task CanMoveArticleFromUnreadToCustomFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.MoveArticleToFolderAsync(article.Id, this.CustomFolder1!.LocalId);

            // Check it's in the destination
            var customArticles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Equal(1, customArticles.Count);
            Assert.Contains(customArticles, (a) => a.Id == article.Id);

            // Check it's not present in unread
            var unreadArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadArticles);
        }

        [Fact]
        public async Task CanMoveArticlesFromUnreadToArchiveFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.MoveArticleToFolderAsync(article.Id, this.db!.ArchiveFolderLocalId);

            // Check it's in the destination
            var archivedArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.ArchiveFolderLocalId);
            Assert.Equal(1, archivedArticles.Count);
            Assert.Contains(archivedArticles, (b) => b.Id == article.Id);

            // Check it's not present in unread
            var unreadArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadArticles);
        }

        [Fact]
        public async Task CanMoveArticleFromCustomFolderToUnread()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            await this.db!.MoveArticleToFolderAsync(article.Id, this.db!.UnreadFolderLocalId);

            // Check it's in the destination
            var unreadArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadArticles.Count);
            Assert.Contains(unreadArticles, (b) => b.Id == article.Id);

            // Check it's not present in unread
            var customArticles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1!.LocalId);
            Assert.Empty(customArticles);
        }

        [Fact]
        public async Task CanMoveArticleFromArchiveToUnread()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.ArchiveFolderLocalId);
            await this.db!.MoveArticleToFolderAsync(article.Id, this.db!.UnreadFolderLocalId);

            // Check it's in the destination
            var unreadArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Equal(1, unreadArticles.Count);
            Assert.Contains(unreadArticles, (b) => b.Id == article.Id);

            // Check it's not present in unread
            var archiveArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.ArchiveFolderLocalId);
            Assert.Empty(archiveArticles);
        }

        [Fact]
        public async Task MovingArticleFromUnreadToNonExistantFolderThrows()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveArticleToFolderAsync(article.Id, 999));
        }

        [Fact]
        public async Task MovingArticleToAFolderItIsAlreadyInSucceeds()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            await this.db!.MoveArticleToFolderAsync(article.Id, this.CustomFolder1!.LocalId);

            var customArticles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1!.LocalId);
            Assert.Equal(1, customArticles.Count);
            Assert.Contains(customArticles, (f) => f.Id == article.Id);
        }

        [Fact]
        public async Task MovingNonExistantArticleToCustomFolder()
        {
            await Assert.ThrowsAsync<ArticleNotFoundException>(() => this.db!.MoveArticleToFolderAsync(999, this.CustomFolder1!.LocalId));
        }

        [Fact]
        public async Task MovingNonExistantArticleToNonExistantFolder()
        {
            await Assert.ThrowsAsync<FolderNotFoundException>(() => this.db!.MoveArticleToFolderAsync(999, 888));
        }

        [Fact]
        public async Task DeletingFolderContainingArticleRemovesFolder()
        {
            _ = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            _ = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);

            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            var folders = await this.db!.ListAllFoldersAsync();
            Assert.DoesNotContain(folders, (f) => f.LocalId == this.CustomFolder1!.LocalId);
        }

        [Fact]
        public async Task CanDeleteArticleInUnreadFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);
            await this.db!.DeleteArticleAsync(article.Id);

            var unreadArticles = await this.db!.ListArticlesForLocalFolderAsync(this.db!.UnreadFolderLocalId);
            Assert.Empty(unreadArticles);
        }

        [Fact]
        public async Task CanDeleteArticleInCustomFolder()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteArticleAsync(article.Id);

            var customArticles = await this.db!.ListArticlesForLocalFolderAsync(this.CustomFolder1.LocalId);
            Assert.Empty(customArticles);
        }

        [Fact]
        public async Task CanDeleteNonExistantArticle()
        {
            await this.db!.DeleteArticleAsync(999);
        }

        [Fact]
        public async Task CanDeleteOrphanedArticle()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);
            await this.db!.DeleteArticleAsync(article.Id);
        }

        [Fact]
        public async Task CanGetArticle()
        {
            var article = await this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
            await this.db!.DeleteFolderAsync(this.CustomFolder1!.LocalId);

            var orphaned = await this.db!.GetArticleByIdAsync(article.Id);
            Assert.NotNull(orphaned);
            Assert.Equal(article.Id, orphaned!.Id);
        }

        [Fact]
        public async Task CanUpdateArticlekWithFullSetOfInformation()
        {
            // Get article
            var article = await this.AddRandomArticleToFolder(this.db!.UnreadFolderLocalId);

            // Update article with new title
            var newTitle = "New Title";
            var updatedArticle = await this.db.UpdateArticleAsync(new(article.Id, newTitle, article.Url, article.Description, article.ReadProgress, article.ReadProgressTimestamp, article.Hash, article.Liked));

            // Check returned values are correct
            Assert.Equal(article.Id, updatedArticle.Id);
            Assert.Equal(newTitle, updatedArticle.Title);
            Assert.Equal(article.Description, updatedArticle.Description);
            Assert.Equal(article.ReadProgress, updatedArticle.ReadProgress);
            Assert.Equal(article.ReadProgressTimestamp, updatedArticle.ReadProgressTimestamp);
            Assert.Equal(article.Hash, updatedArticle.Hash);
            Assert.Equal(article.Liked, updatedArticle.Liked);
            Assert.Equal(article.HasLocalState, updatedArticle.HasLocalState);

            // Get from database again and check them
            var retreivedArticle = (await this.db!.GetArticleByIdAsync(article.Id))!;
            Assert.Equal(article.Id, retreivedArticle.Id);
            Assert.Equal(newTitle, retreivedArticle.Title);
            Assert.Equal(article.Description, retreivedArticle.Description);
            Assert.Equal(article.ReadProgress, retreivedArticle.ReadProgress);
            Assert.Equal(article.ReadProgressTimestamp, retreivedArticle.ReadProgressTimestamp);
            Assert.Equal(article.Hash, retreivedArticle.Hash);
            Assert.Equal(article.Liked, retreivedArticle.Liked);
            Assert.Equal(article.HasLocalState, retreivedArticle.HasLocalState);
        }

        [Fact]
        public async Task UpdatingArticleThatDoesntExistFails()
        {
            await Assert.ThrowsAsync<ArticleNotFoundException>(async () =>
            {
                _ = await db!.UpdateArticleAsync(
                    new(99, String.Empty, new Uri("https://www.bing.com"), String.Empty, 0.0F, DateTime.Now, String.Empty, false)
                );
            });
        }
    }
}