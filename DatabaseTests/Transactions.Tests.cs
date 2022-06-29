using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderTransactionTests : IDisposable
{
    private IInstapaperDatabase instapaperDb;
    private IFolderDatabaseWithTransactionEvents db;

    public FolderTransactionTests()
    {
        this.instapaperDb = TestUtilities.GetDatabase();
        this.db = (IFolderDatabaseWithTransactionEvents)this.instapaperDb.FolderDatabase;
    }

    public void Dispose()
    {
        this.instapaperDb.Dispose();
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringFolderCreationAddEventRollsBackEntireChange()
    {
        this.db.FolderAddedWithinTransaction += (_, folder) =>
        {
            var added = this.db.GetFolderByTitle(folder)!;
            this.instapaperDb.FolderChangesDatabase.CreatePendingFolderAdd(added.LocalId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.CreateFolder("Sample"));

        Assert.Equal(2, this.db.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb.FolderChangesDatabase.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringWillDeleteFolderEventRollsBackEntireChange()
    {
        var createdFolder = this.db.AddKnownFolder("Sample", 10L, 1L, true);
        this.db.FolderWillBeDeletedWithinTransaction += (_, folder) =>
        {
            this.instapaperDb.FolderChangesDatabase.CreatePendingFolderDelete((createdFolder).ServiceId!.Value, createdFolder.Title);
            this.ThrowException();
        };
        Assert.Throws<Exception>(() => this.db.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(3, this.db.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb.FolderChangesDatabase.ListPendingFolderDeletes());
    }
}

public sealed class ArticleTransactionTests : IDisposable
{
    private IInstapaperDatabase instapaperDb;
    private IArticleDatabaseWithTransactionEvents db;
    private DatabaseFolder CustomFolder1;

    public ArticleTransactionTests()
    {
        this.instapaperDb = TestUtilities.GetDatabase();
        this.db = (IArticleDatabaseWithTransactionEvents)this.instapaperDb.ArticleDatabase;

        // Add sample folders
        this.CustomFolder1 = this.instapaperDb.FolderDatabase.AddKnownFolder(title: "Sample1",
                                                              serviceId: 9L,
                                                              position: 1,
                                                              shouldSync: true);
    }

    public void Dispose()
    {
        this.instapaperDb.Dispose();
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringArticleLikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, article) =>
        {
            this.instapaperDb.ArticleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.LikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.instapaperDb.ArticleChangesDatabase.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleUnlikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle() with { liked = true };

        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, article) =>
        {
            this.instapaperDb.ArticleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.UnlikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.instapaperDb.ArticleChangesDatabase.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleMovedEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleMovedToFolderWithinTransaction += (_, payload) =>
        {
            this.instapaperDb.ArticleChangesDatabase.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.MoveArticleToFolder(randomArticle.id, this.CustomFolder1.LocalId));

        Assert.Empty(this.db.ListArticlesForLocalFolder(this.CustomFolder1.LocalId));
        Assert.Equal(1, this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Count);
    }

    [Fact]
    public void ExceptionDuringArticleDeleteEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        this.db.AddLocalOnlyStateForArticle(new() { ArticleId = randomArticle.id });

        this.db.ArticleDeletedWithinTransaction += (_, articleId) =>
        {
            this.instapaperDb.ArticleChangesDatabase.CreatePendingArticleDelete(articleId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.DeleteArticle(randomArticle.id));

        Assert.Equal(1, this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Count);

        var localState = this.db.GetLocalOnlyStateByArticleId(randomArticle.id);
        Assert.NotNull(localState);
    }
}