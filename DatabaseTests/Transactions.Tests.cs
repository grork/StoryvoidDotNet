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

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringFolderCreationAddEventRollsBackEntireChange()
    {
        this.db.FolderAddedWithinTransaction += (_, folder) =>
        {
            var added = this.db.GetFolderByTitle(folder)!;
            this.folderChanges.CreatePendingFolderAdd(added.LocalId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.CreateFolder("Sample"));

        Assert.Equal(2, this.db.ListAllFolders().Count);
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
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

        Assert.Equal(3, this.db.ListAllFolders().Count);
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
    }

    [Fact]
    public void ExceptionDuringFolderDeletedEventRollsBackEntireChange()
    {
        var createdFolder = this.db.AddKnownFolder("Sample", 10L, 1L, true);
        this.db.FolderWillBeDeletedWithinTransaction += (_, folder) => this.folderChanges.CreatePendingFolderDelete((createdFolder).ServiceId!.Value, createdFolder.Title);
        this.db.FolderDeletedWithinTransaction += (_, _) => this.ThrowException();
        
        Assert.Throws<Exception>(() => this.db.DeleteFolder(createdFolder.LocalId));

        Assert.Equal(3, this.db.ListAllFolders().Count);
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
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

    private void ThrowException() => throw new Exception("Sample Exception");

    [Fact]
    public void ExceptionDuringArticleLikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, article) =>
        {
            this.articleChanges.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.LikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleUnlikingEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle() with { liked = true };

        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleLikeStatusChangedWithinTransaction += (_, article) =>
        {
            this.articleChanges.CreatePendingArticleStateChange(article.Id, article.Liked);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.UnlikeArticle(randomArticle.id));

        var retreivedArticle = this.db.GetArticleById(randomArticle.id)!;
        Assert.Equal(randomArticle.liked ,retreivedArticle.Liked);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void ExceptionDuringArticleMovedEventRollsBackEntireChange()
    {
        var randomArticle = TestUtilities.GetRandomArticle();
        this.db.AddArticleToFolder(randomArticle, WellKnownLocalFolderIds.Unread);

        this.db.ArticleMovedToFolderWithinTransaction += (_, payload) =>
        {
            this.articleChanges.CreatePendingArticleMove(payload.Article.Id, payload.DestinationLocalFolderId);
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
            this.articleChanges.CreatePendingArticleDelete(articleId);
            this.ThrowException();
        };

        Assert.Throws<Exception>(() => this.db.DeleteArticle(randomArticle.id));

        Assert.Equal(1, this.db.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).Count);

        var localState = this.db.GetLocalOnlyStateByArticleId(randomArticle.id);
        Assert.NotNull(localState);
    }
}