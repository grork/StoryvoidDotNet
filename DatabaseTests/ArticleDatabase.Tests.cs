using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleDatabaseTests : IAsyncLifetime
{
    private static readonly Uri BASE_URI = new("https://www.bing.com");
    private IArticleDatabase? db;
    private IInstapaperDatabase? instapaperDb;
    private DatabaseFolder? CustomFolder1;
    private DatabaseFolder? CustomFolder2;

    public async Task InitializeAsync()
    {
        this.instapaperDb = await TestUtilities.GetDatabase();
        this.db = this.instapaperDb.ArticleDatabase;

        // Add sample folders
        this.CustomFolder1 = this.instapaperDb.FolderDatabase.AddKnownFolder(title: "Sample1",
                                                              serviceId: 9L,
                                                              position: 1,
                                                              shouldSync: true);

        this.CustomFolder2 = this.instapaperDb.FolderDatabase.AddKnownFolder(title: "Sample2",
                                                               serviceId: 10L,
                                                               position: 1,
                                                               shouldSync: true);
    }

    public Task DisposeAsync()
    {
        this.instapaperDb?.Dispose();
        return Task.CompletedTask;
    }

    private int nextArticleId = 0;
    private ArticleRecordInformation GetRandomArticle()
    {
        return new(
            id: nextArticleId++,
            title: "Sample Article",
            url: new Uri(BASE_URI, $"/{nextArticleId}"),
            description: String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "ABC",
            liked: false
        );
    }

    private DatabaseArticle AddRandomArticleToFolder(long localFolderId)
    {
        var article = this.db!.AddArticleToFolder(
            this.GetRandomArticle(),
            localFolderId
        );

        return article;
    }

    [Fact]
    public void CanListArticlesWhenEmpty()
    {
        var articles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Empty(articles);
    }

    [Fact]
    public void CanAddArticles()
    {
        var a = this.GetRandomArticle();
        var result = this.db!.AddArticleToFolder(a, WellKnownLocalFolderIds.Unread);

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
    public void CanGetSingleArticle()
    {
        var a = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var retrievedArticle = (this.db!.GetArticleById(a.Id))!;
        Assert.Equal(a.ReadProgressTimestamp, retrievedArticle.ReadProgressTimestamp);
        Assert.Equal(a.Title, retrievedArticle.Title);
        Assert.Equal(a.Url, retrievedArticle.Url);
        Assert.Equal(a.Hash, retrievedArticle.Hash);

        // Don't expect local state since we have a fresh set of articles
        Assert.False(retrievedArticle.HasLocalState);
    }

    [Fact]
    public void GettingNonExistantArticleReturnsNull()
    {
        var missingArticle = this.db!.GetArticleById(1);
        Assert.Null(missingArticle);
    }

    [Fact]
    public void CanListArticlesInUnreadFolder()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var articles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
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
    public void CanAddArticleToSpecificFolder()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);

        var articles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1.LocalId);
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
    public void AddingArticleToNonExistantFolderFails()
    {
        var article = this.GetRandomArticle();
        Assert.Throws<FolderNotFoundException>(() =>
        {
            _ = this.db!.AddArticleToFolder(article, 999L);
        });
    }

    [Fact]
    public void ArticlesAreOnlyReturnedInTheirOwningFolders()
    {
        var customFolderArticle = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        var unreadFolderArticle = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var customFolderArticles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1.LocalId);
        Assert.Equal(1, customFolderArticles.Count);
        Assert.Contains(customFolderArticles, (b) => b.Id == customFolderArticle.Id);

        var customArticleFromListing = customFolderArticles.First();
        Assert.Equal(customFolderArticle.ReadProgressTimestamp, customArticleFromListing.ReadProgressTimestamp);
        Assert.Equal(customFolderArticle.Title, customArticleFromListing.Title);
        Assert.Equal(customFolderArticle.Url, customArticleFromListing.Url);
        Assert.Equal(customFolderArticle.Hash, customArticleFromListing.Hash);
        Assert.False(customArticleFromListing.HasLocalState);

        var unreadFolderArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
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
    public void ListingLikedArticlesWithNoLikedArticlesReturnsEmptyList()
    {
        var likedArticles = this.db!.ListLikedArticle();
        Assert.Empty(likedArticles);
    }

    [Fact]
    public void CanLikeArticleThatIsUnliked()
    {
        var unlikedArticle = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);
        var likedArticle = this.db!.LikeArticle(unlikedArticle.Id);
        Assert.Equal(unlikedArticle.Id, likedArticle.Id);
        Assert.True(likedArticle.Liked);
    }

    [Fact]
    public void LikingArticleThatIsUnlikedRaisesLikeStatusChangedEvent()
    {
        var unlikedArticle = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        DatabaseArticle? eventChangeArticle = null;
        this.db!.ArticleLikeStatusChanged += (_, article) => eventChangeArticle = article;

        var likedArticle = this.db!.LikeArticle(unlikedArticle.Id);
        Assert.Equal(likedArticle, eventChangeArticle);
        Assert.True(eventChangeArticle!.Liked);
    }

    [Fact]
    public void CanListOnlyLikedArticle()
    {
        var article = this.GetRandomArticle() with { liked = true };

        _ = this.db!.AddArticleToFolder(
            article,
            WellKnownLocalFolderIds.Unread
        );

        var likedArticles = this.db!.ListLikedArticle();
        Assert.Equal(1, likedArticles.Count);
        Assert.Contains(likedArticles, (a) => (a.Id == article.id) && a.Liked);
    }

    [Fact]
    public void ListingLikedArticlesReturnsResultsAcrossFolders()
    {
        var article1 = this.GetRandomArticle() with { liked = true };
        _ = this.db!.AddArticleToFolder(article1, WellKnownLocalFolderIds.Unread);

        var article2 = this.GetRandomArticle() with { liked = true };
        _ = this.db!.AddArticleToFolder(article2, this.CustomFolder1!.LocalId);

        var likedArticles = this.db!.ListLikedArticle();
        Assert.Equal(2, likedArticles.Count);
        Assert.Contains(likedArticles, (a) => (a.Id == article1.id) && a.Liked);
        Assert.Contains(likedArticles, (a) => (a.Id == article2.id) && a.Liked);
    }

    [Fact]
    public void CanUnlikeArticleThatIsLiked()
    {
        var a = this.GetRandomArticle() with { liked = true };
        var likedArticle = this.db!.AddArticleToFolder(a, WellKnownLocalFolderIds.Unread);

        var unlikedArticle = this.db!.UnlikeArticle(likedArticle.Id);
        Assert.Equal(likedArticle.Id, unlikedArticle.Id);
        Assert.False(unlikedArticle.Liked);
    }

    [Fact]
    public void UnlikingArticleThatIsLikedRaisesLikeStatusChangedEvent()
    {
        var originalArticle = this.db!.AddArticleToFolder(this.GetRandomArticle() with { liked = true }, WellKnownLocalFolderIds.Unread);

        DatabaseArticle? eventChangeArticle = null;
        this.db!.ArticleLikeStatusChanged += (_, article) => eventChangeArticle = article;

        var unlikedArticle = this.db!.UnlikeArticle(originalArticle.Id);
        Assert.Equal(unlikedArticle, eventChangeArticle);
        Assert.False(eventChangeArticle!.Liked);
    }

    [Fact]
    public void LikingMissingArticleThrows()
    {
        Assert.Throws<ArticleNotFoundException>(() => this.db!.LikeArticle(1));
    }

    [Fact]
    public void LikingMissingArticleDoesNotRaiseLikeStatusChangeEvent()
    {
        var eventWasRaised = false;
        this.db!.ArticleLikeStatusChanged += (_, _) => eventWasRaised = true;
        Assert.Throws<ArticleNotFoundException>(() => this.db!.LikeArticle(1));
        Assert.False(eventWasRaised);
    }

    [Fact]
    public void UnlikingMissingArticleThrows()
    {
        Assert.Throws<ArticleNotFoundException>(() => this.db!.UnlikeArticle(1));
    }

    [Fact]
    public void UnlikingMissingArticleDoesNotRaiseLikeStatusChangeEvent()
    {
        var eventWasRaised = false;
        this.db!.ArticleLikeStatusChanged += (_, _) => eventWasRaised = true;
        Assert.Throws<ArticleNotFoundException>(() => this.db!.UnlikeArticle(1));
        Assert.False(eventWasRaised);
    }

    [Fact]
    public void LikingArticleThatIsLikedSucceeds()
    {
        var a = this.GetRandomArticle() with { liked = true };
        var likedArticleOriginal = this.db!.AddArticleToFolder(a, WellKnownLocalFolderIds.Unread);
        var likedArticle = this.db!.LikeArticle(likedArticleOriginal.Id);

        Assert.Equal(likedArticleOriginal.Id, likedArticle.Id);
        Assert.True(likedArticle.Liked);
    }

    [Fact]
    public void LikingArticleThatIsLikedDoesNotRaiseLikeStatusChangeEvent()
    {
        var a = this.GetRandomArticle() with { liked = true };
        var likedArticleOriginal = this.db!.AddArticleToFolder(a, WellKnownLocalFolderIds.Unread);

        var eventWasRaised = false;
        this.db!.ArticleLikeStatusChanged += (_, _) => eventWasRaised = true;

        _ = this.db!.LikeArticle(likedArticleOriginal.Id);
        Assert.False(eventWasRaised);
    }

    [Fact]
    public void UnlikingArticleThatIsNotLikedSucceeds()
    {
        var unlikedArticleOriginal = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var eventWasRaised = false;
        this.db!.ArticleLikeStatusChanged += (_, _) => eventWasRaised = true;
        _ = this.db!.UnlikeArticle(unlikedArticleOriginal.Id);
        Assert.False(eventWasRaised);
    }

    [Fact]
    public void CanUpdateArticleProgressWithTimeStamp()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var progressTimestamp = DateTime.Now.AddMinutes(5);
        var progress = 0.3F;
        DatabaseArticle updatedArticle = this.db!.UpdateReadProgressForArticle(progress, progressTimestamp, article.Id);
        Assert.Equal(article.Id, updatedArticle.Id);
        Assert.Equal(progressTimestamp, updatedArticle.ReadProgressTimestamp);
        Assert.Equal(progress, updatedArticle.ReadProgress);
        Assert.NotEqual(article.Hash, updatedArticle.Hash);
    }

    [Fact]
    public void ProgressUpdateChangesReflectedInListCall()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        var beforeUpdate = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Equal(1, beforeUpdate.Count);
        Assert.Contains(beforeUpdate, (a) =>
            (a.Id == article.Id) && a.ReadProgress == article.ReadProgress && a.ReadProgressTimestamp == article.ReadProgressTimestamp);

        var progressTimestamp = DateTime.Now.AddMinutes(5);
        var progress = 0.3F;
        article = this.db!.UpdateReadProgressForArticle(progress, progressTimestamp, article.Id);
        var afterUpdate = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Equal(1, afterUpdate.Count);
        Assert.Contains(afterUpdate, (a) =>
            (a.Id == article.Id) && a.ReadProgress == progress && a.ReadProgressTimestamp == progressTimestamp);
    }

    [Fact]
    public void UpdatingProgressOfNonExistantArticleThrows()
    {
        Assert.Throws<ArticleNotFoundException>(() =>
        {
            this.db!.UpdateReadProgressForArticle(0.4F, DateTime.Now, 1);
        });
    }

    [Fact]
    public void UpdatingProgressOutsideSupportedRangeThrows()
    {
        _ = this.db!.AddArticleToFolder(
            this.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            this.db!.UpdateReadProgressForArticle(-0.01F, DateTime.Now, 1);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            this.db!.UpdateReadProgressForArticle(1.01F, DateTime.Now, 1);
        });
    }

    [Fact]
    public void UpdatingProgressWithTimeStampOutsideUnixEpochThrows()
    {
        _ = this.db!.AddArticleToFolder(
            this.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            this.db!.UpdateReadProgressForArticle(0.5F, new DateTime(1969, 12, 31, 23, 59, 59), 1);
        });
    }

    [Fact]
    public void CanMoveArticleFromUnreadToCustomFolder()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);
        this.db!.MoveArticleToFolder(article.Id, this.CustomFolder1!.LocalId);

        // Check it's in the destination
        var customArticles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1.LocalId);
        Assert.Equal(1, customArticles.Count);
        Assert.Contains(customArticles, (a) => a.Id == article.Id);

        // Check it's not present in unread
        var unreadArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Empty(unreadArticles);
    }

    [Fact]
    public void CanMoveArticlesFromUnreadToArchiveFolder()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);
        this.db!.MoveArticleToFolder(article.Id, WellKnownLocalFolderIds.Archive);

        // Check it's in the destination
        var archivedArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Equal(1, archivedArticles.Count);
        Assert.Contains(archivedArticles, (b) => b.Id == article.Id);

        // Check it's not present in unread
        var unreadArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Empty(unreadArticles);
    }

    [Fact]
    public void CanMoveArticleFromCustomFolderToUnread()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        this.db!.MoveArticleToFolder(article.Id, WellKnownLocalFolderIds.Unread);

        // Check it's in the destination
        var unreadArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Equal(1, unreadArticles.Count);
        Assert.Contains(unreadArticles, (b) => b.Id == article.Id);

        // Check it's not present in unread
        var customArticles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1!.LocalId);
        Assert.Empty(customArticles);
    }

    [Fact]
    public void CanMoveArticleFromArchiveToUnread()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Archive);
        this.db!.MoveArticleToFolder(article.Id, WellKnownLocalFolderIds.Unread);

        // Check it's in the destination
        var unreadArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Equal(1, unreadArticles.Count);
        Assert.Contains(unreadArticles, (b) => b.Id == article.Id);

        // Check it's not present in unread
        var archiveArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        Assert.Empty(archiveArticles);
    }

    [Fact]
    public void MovingArticleFromUnreadToNonExistantFolderThrows()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);
        Assert.Throws<FolderNotFoundException>(() => this.db!.MoveArticleToFolder(article.Id, 999));
    }

    [Fact]
    public void MovingArticleToAFolderItIsAlreadyInSucceeds()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        this.db!.MoveArticleToFolder(article.Id, this.CustomFolder1!.LocalId);

        var customArticles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1!.LocalId);
        Assert.Equal(1, customArticles.Count);
        Assert.Contains(customArticles, (f) => f.Id == article.Id);
    }

    [Fact]
    public void MovingNonExistantArticleToCustomFolder()
    {
        Assert.Throws<ArticleNotFoundException>(() => this.db!.MoveArticleToFolder(999, this.CustomFolder1!.LocalId));
    }

    [Fact]
    public void MovingNonExistantArticleToNonExistantFolder()
    {
        Assert.Throws<FolderNotFoundException>(() => this.db!.MoveArticleToFolder(999, 888));
    }

    [Fact]
    public void DeletingFolderContainingArticleRemovesFolder()
    {
        _ = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        _ = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);

        this.instapaperDb!.FolderDatabase.DeleteFolder(this.CustomFolder1!.LocalId);
        var folders = this.instapaperDb!.FolderDatabase.ListAllFolders();
        Assert.DoesNotContain(folders, (f) => f.LocalId == this.CustomFolder1!.LocalId);
    }

    [Fact]
    public void CanDeleteArticleInUnreadFolder()
    {
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);
        this.db!.DeleteArticle(article.Id);

        var unreadArticles = this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        Assert.Empty(unreadArticles);
    }

    [Fact]
    public void CanDeleteArticleInCustomFolder()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        this.db!.DeleteArticle(article.Id);

        var customArticles = this.db!.ListArticlesForLocalFolder(this.CustomFolder1.LocalId);
        Assert.Empty(customArticles);
    }

    [Fact]
    public void CanDeleteNonExistantArticle()
    {
        this.db!.DeleteArticle(999);
    }

    [Fact]
    public void CanDeleteOrphanedArticle()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        this.instapaperDb!.FolderDatabase.DeleteFolder(this.CustomFolder1!.LocalId);
        this.db!.DeleteArticle(article.Id);
    }

    [Fact]
    public void CanGetArticle()
    {
        var article = this.AddRandomArticleToFolder(this.CustomFolder1!.LocalId);
        this.instapaperDb!.FolderDatabase.DeleteFolder(this.CustomFolder1!.LocalId);

        var orphaned = this.db!.GetArticleById(article.Id);
        Assert.NotNull(orphaned);
        Assert.Equal(article.Id, orphaned!.Id);
    }

    [Fact]
    public void CanUpdateArticleWithFullSetOfInformation()
    {
        // Get article
        var article = this.AddRandomArticleToFolder(WellKnownLocalFolderIds.Unread);

        // Update article with new title
        var newTitle = "New Title";
        var updatedArticle = this.db!.UpdateArticle(new(article.Id, newTitle, article.Url, article.Description, article.ReadProgress, article.ReadProgressTimestamp, article.Hash, article.Liked));

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
        var retreivedArticle = (this.db!.GetArticleById(article.Id))!;
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
    public void UpdatingArticleThatDoesntExistFails()
    {
        Assert.Throws<ArticleNotFoundException>(() =>
        {
            _ = db!.UpdateArticle(
                new(99, String.Empty, new Uri("https://www.bing.com"), String.Empty, 0.0F, DateTime.Now, String.Empty, false)
            );
        });
    }
}