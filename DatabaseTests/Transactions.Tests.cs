using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderTransactionTests : IAsyncLifetime
{
    private IInstapaperDatabase? instapaperDb;
    private IFolderDatabase? db;

    public async Task InitializeAsync()
    {
        this.instapaperDb = await TestUtilities.GetDatabase();
        this.db = this.instapaperDb.FolderDatabase;
    }

    public Task DisposeAsync()
    {
        this.instapaperDb?.Dispose();
        return Task.CompletedTask;
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringFolderCreationAddEventRollsBackEntireChange()
    {
        this.db!.FolderAdded += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderAdd(folder.LocalId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.CreateFolder("Sample"));

        Assert.Equal(2, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringAddingKnownFolderAddEventRollsBackEntireChange()
    {
        this.db!.FolderAdded += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderAdd(folder.LocalId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.AddKnownFolder("Sample", 1L, 1L, true));

        Assert.Equal(2, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringWillDeleteFolderEventRollsBackEntireChange()
    {
        var createdFolder = this.db!.AddKnownFolder("Sample", 10L, 1L, true);
        this.db!.FolderWillBeDeleted += (_, folder) =>
        {
            this.instapaperDb!.FolderChangesDatabase.CreatePendingFolderDelete((createdFolder!).ServiceId!.Value, createdFolder.Title);
            this.ThrowException();
        };
        Assert.Throws<Exception>(() => this.db!.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(3, this.db!.ListAllFolders().Count);
        Assert.Empty(this.instapaperDb!.FolderChangesDatabase.ListPendingFolderDeletes());
    }
}

public sealed class ArticleTransactionTests : IAsyncLifetime
{
    private IInstapaperDatabase? instapaperDb;
    private IArticleDatabase? db;
    private DatabaseFolder? CustomFolder1;

    public async Task InitializeAsync()
    {
        this.instapaperDb = await TestUtilities.GetDatabase();
        this.db = this.instapaperDb.ArticleDatabase;

        // Add sample folders
        this.CustomFolder1 = this.instapaperDb.FolderDatabase.AddKnownFolder(title: "Sample1",
                                                              serviceId: 9L,
                                                              position: 1,
                                                              shouldSync: true);
    }

    public Task DisposeAsync()
    {
        this.instapaperDb?.Dispose();
        return Task.CompletedTask;
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringArticleLikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db!.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db!.ArticleLikeStatusChanged += (_, article) =>
        {
            this.instapaperDb!.ArticleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.LikeArticle(randomArticle.id));

        var retreivedArticle = this.db!.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.instapaperDb!.ArticleChangesDatabase.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleUnlikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle() with { liked = true };

        this.db!.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db!.ArticleLikeStatusChanged += (_, article) =>
        {
            this.instapaperDb!.ArticleChangesDatabase.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.UnlikeArticle(randomArticle.id));

        var retreivedArticle = this.db!.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.instapaperDb!.ArticleChangesDatabase.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleMovedEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db!.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db!.ArticleMovedToFolder += (_, payload) =>
        {
            this.instapaperDb!.ArticleChangesDatabase.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.MoveArticleToFolder(randomArticle.id, this.CustomFolder1!.LocalId));

        Assert.Empty(this.db!.ListArticlesForLocalFolder(this.CustomFolder1!.LocalId));
        Assert.Equal(1, this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Count);
    }

    [Fact]
    public void ExceptionDuringArticleDeleteEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db!.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        this.db!.AddLocalOnlyStateForArticle(new() { ArticleId = randomArticle.id });

        this.db!.ArticleDeleted += (_, articleId) =>
        {
            this.instapaperDb!.ArticleChangesDatabase.CreatePendingArticleDelete(articleId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db!.DeleteArticle(randomArticle.id));

        Assert.Equal(1, this.db!.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Count);

        var localState = this.db!.GetLocalOnlyStateByArticleId(randomArticle.id);
        Assert.NotNull(localState);
    }
}