using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderTransactionTests : IDisposable
{
    private IDbConnection connection;
    private IFolderDatabaseWithTransactionEvents db;
    private IFolderChangesDatabase folderChanges;

    public FolderTransactionTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.db = new FolderDatabase(this.connection);
        this.folderChanges = new FolderChanges(this.connection);
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    private DatabaseEventClearingHouse SwitchToDatabaseEventing()
    {
        var clearingHouse = new DatabaseEventClearingHouse();
        this.db = new FolderDatabase(this.connection, clearingHouse);
        return clearingHouse;
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringFolderCreationAddEventRollsBackEntireChange()
    {
        this.db.FolderAddedWithinTransaction += (_, payload) =>
        {
            var folder = payload.Data;
            var added = this.db.GetFolderByTitle(folder)!;
            this.folderChanges.CreatePendingFolderAdd(added.LocalId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.CreateFolder("Sample"));

        Assert.Equal(2, this.db.ListAllFolders().Count());
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringFolderAddClearingHouseEventDoesNotRollBackEntireChange()
    {
        var clearingHouse = this.SwitchToDatabaseEventing();

        this.db.FolderAddedWithinTransaction += (_, payload) =>
        {
            var folder = payload.Data;
            var added = this.db.GetFolderByTitle(folder)!;
            this.folderChanges.CreatePendingFolderAdd(added.LocalId);
        };

        clearingHouse.FolderAdded += (_, _) => this.ThrowException();

        Assert.Throws<Exception>(() => this.db.CreateFolder("Sample"));

        Assert.Equal(3, this.db.ListAllFolders().Count());
        Assert.Single(this.folderChanges.ListPendingFolderAdds());
    }

    [Fact]
    public void ExceptionDuringWillDeleteFolderEventRollsBackEntireChange()
    {
        var createdFolder = this.db.AddKnownFolder("Sample", 10L, 1L, true);
        this.db.FolderWillBeDeletedWithinTransaction += (_, folder) =>
        {
            this.folderChanges.CreatePendingFolderDelete((createdFolder).ServiceId!.Value, createdFolder.Title);
            this.ThrowException();
        };
        Assert.Throws<Exception>(() => this.db.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(3, this.db.ListAllFolders().Count());
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
    }

    [Fact]
    public void ExceptionDuringFolderDeletedClearingHouseEventDoesNotRollBackEntireChange()
    {
        var createdFolder = this.db.AddKnownFolder("Sample", 10L, 1L, true);
        var clearingHouse = this.SwitchToDatabaseEventing();
        this.db.FolderWillBeDeletedWithinTransaction += (_, folder) => this.folderChanges.CreatePendingFolderDelete((createdFolder).ServiceId!.Value, createdFolder.Title);
        clearingHouse.FolderDeleted += (_, __) => this.ThrowException();

        Assert.Throws<Exception>(() => this.db.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(2, this.db.ListAllFolders().Count());
        Assert.Single(this.folderChanges.ListPendingFolderDeletes());
    }
}

public sealed class ArticleTransactionTests : IDisposable
{
    private IDbConnection connection;
    private IArticleDatabaseWithTransactionEvents db;
    private IArticleChangesDatabase articleChanges;
    private DatabaseFolder CustomFolder1;

    public ArticleTransactionTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.db = new ArticleDatabase(this.connection);
        this.articleChanges = new ArticleChanges(this.connection);

        // Add sample folders
        this.CustomFolder1 = new FolderDatabase(this.connection).AddKnownFolder(title: "Sample1",
                                                                                serviceId: 9L,
                                                                                position: 1,
                                                                                shouldSync: true);
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    private DatabaseEventClearingHouse SwitchToDatabaseEventing()
    {
        var clearingHouse = new DatabaseEventClearingHouse();
        this.db = new ArticleDatabase(this.connection, clearingHouse);
        return clearingHouse;
    }

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringArticleAddedClearingHousEventDoesNotRollBackChange()
    {
        var clearingHouse = this.SwitchToDatabaseEventing();
        clearingHouse.ArticleAdded += (_, _) => this.ThrowException();

        Assert.Empty(this.db.ListAllArticlesInAFolder());

        var articleToAdd = TestUtilities.GetRandomArticle();
        Assert.Throws<Exception>(() => this.db.AddArticleToFolder(articleToAdd, WellKnownLocalFolderIds.Unread));

        var articles = this.db.ListAllArticlesInAFolder();
        Assert.Single(articles);
        Assert.Equal(articleToAdd.id, articles.First()!.Article.Id);
    }
    
    [Fact]
    public void ExceptionDuringArticleLikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, payload) =>
        {
            var article = payload.Data;
            this.articleChanges.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.LikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked, retreivedArticle.Liked);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleUpdatedClearingHouseEventDoesNotRollBackChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        var clearingHouse = this.SwitchToDatabaseEventing();

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, payload) =>
        {
            var article = payload.Data;
            this.articleChanges.CreatePendingArticleStateChange(article.Id, article.Liked);
        };

        clearingHouse.ArticleUpdated += (_, _) => this.ThrowException();

        Assert.Throws<Exception>(() => this.db.LikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.NotEqual(randomArticle.liked, retreivedArticle.Liked);
        Assert.Single(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleUnlikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle() with { liked = true };
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, payload) =>
        {
            var article = payload.Data;
            this.articleChanges.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.UnlikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked, retreivedArticle.Liked);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleMovedEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleMovedToFolderWithinTransaction += (_, args) =>
        {
            var payload = args.Data;
            this.articleChanges.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.MoveArticleToFolder(randomArticle.id, this.CustomFolder1.LocalId));

        Assert.Empty(this.db.ListArticlesForLocalFolder(this.CustomFolder1.LocalId));
        Assert.Single(this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread));
    }

    [Fact]
    public void ExceptionDuringArticleMovedClearingHouseEventDoesNotRollBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        var clearingHouse = this.SwitchToDatabaseEventing();

        this.db.ArticleMovedToFolderWithinTransaction += (_, args) =>
        {
            var payload = args.Data;
            this.articleChanges.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
        };

        clearingHouse.ArticleMoved += (_, _) => this.ThrowException();

        Assert.Throws<Exception>(() => this.db.MoveArticleToFolder(randomArticle.id, this.CustomFolder1.LocalId));

        Assert.Single(this.db.ListArticlesForLocalFolder(this.CustomFolder1.LocalId));
        Assert.Empty(this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread));
    }

    [Fact]
    public void ExceptionDuringArticleDeleteEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        this.db.AddLocalOnlyStateForArticle(new() { ArticleId = randomArticle.id });

        this.db.ArticleDeletedWithinTransaction += (_, payload) =>
        {
            this.articleChanges.CreatePendingArticleDelete(payload.Data);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.DeleteArticle(randomArticle.id));

        Assert.Single(this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread));

        var localState = this.db.GetLocalOnlyStateByArticleId(randomArticle.id);
        Assert.NotNull(localState);

        Assert.Empty(this.articleChanges.ListPendingArticleDeletes());
    }

    [Fact]
    public void ExceptionDuringArticleDeleteClearingHouseEventDoesNotRollBackBackChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);
        this.db.AddLocalOnlyStateForArticle(new() { ArticleId = randomArticle.id });

        var clearingHouse = this.SwitchToDatabaseEventing();

        this.db.ArticleDeletedWithinTransaction += (_, payload) =>
        {
            this.articleChanges.CreatePendingArticleDelete(payload.Data);
        };

        clearingHouse.ArticleDeleted += (_, _) => this.ThrowException();

        Assert.Throws<Exception>(() => this.db.DeleteArticle(randomArticle.id));

        Assert.Empty(this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread));

        var localState = this.db.GetLocalOnlyStateByArticleId(randomArticle.id);
        Assert.Null(localState);

        Assert.Single(this.articleChanges.ListPendingArticleDeletes());
    }
}