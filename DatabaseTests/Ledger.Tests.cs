using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderLedgerTests : IDisposable
{
    private IDbConnection connection;
    private IFolderDatabase folders;
    private IFolderChangesDatabase folderChanges;
    private Ledger ledger;

    public FolderLedgerTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.folders = new FolderDatabase(this.connection);
        this.folderChanges = new FolderChanges(this.connection);
        this.ledger = new((IFolderDatabaseWithTransactionEvents)this.folders,
                          new ArticleDatabase(this.connection));
    }

    public void Dispose()
    {
        this.ledger.Dispose();
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public void AddsPendingAddWhenFolderIsCreated()
    {
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());

        var folder = this.folders.CreateFolder("Sample");
        var pendingAdds = this.folderChanges.ListPendingFolderAdds();
        Assert.Single(pendingAdds);
        Assert.Equal(folder.Title, pendingAdds[0].Title);
        Assert.Equal(folder.LocalId, pendingAdds[0].FolderLocalId);
    }

    [Fact]
    public void DoesNotAddPendingAddWhenKnownFolderIsAdded()
    {
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());

        var folder = this.folders.AddKnownFolder("Known Folder", 1L, 1L, true);
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
    }

    [Fact]
    public void DeletingKnownFolderCreatesPendingDeleteWithServiceProperties()
    {
        var folder = this.folders.AddKnownFolder("Sample", 1L, 1L, true);

        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
        this.folders.DeleteFolder(folder.LocalId);

        var pendingDeletes = this.folderChanges.ListPendingFolderDeletes();
        Assert.Single(pendingDeletes);
        Assert.Equal(folder.ServiceId, pendingDeletes[0].ServiceId);
        Assert.Equal(folder.Title, pendingDeletes[0].Title);
    }

    [Fact]
    public void DeletingAFolderTwiceDoesNotCreateDuplicatePendingEntries()
    {
        var folder = this.folders.AddKnownFolder("Known Folder", 1L, 1L, true);

        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
        this.folders.DeleteFolder(folder.LocalId);
        this.folders.DeleteFolder(folder.LocalId);
        Assert.Single(this.folderChanges.ListPendingFolderDeletes());
    }

    [Fact]
    public void PendingAddsAreCleanedUpWhenAFolderWithAPendingAddIsDeletedAndDoesntCreateANewDelete()
    {
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
        var folder = this.folders.CreateFolder("Sample");
        Assert.NotEmpty(this.folderChanges.ListPendingFolderAdds());

        this.folders.DeleteFolder(folder.LocalId);

        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
    }

    [Fact]
    public void AddingPreviouslyDeletedKnownFolderResurrectsItsServiceProperties()
    {
        var folder = this.folders.AddKnownFolder("Sample", 1L, 1L, true);

        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
        this.folders.DeleteFolder(folder.LocalId);

        var resurrectedFolder = this.folders.CreateFolder(folder.Title);
        Assert.Equal(folder.ServiceId, resurrectedFolder.ServiceId);

        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());
    }

    [Fact]
    public void DeletingFolderThatHasPendingArticleMoveToItThrowsException()
    {
        // Move to init
        var sampleArticle = new ArticleDatabase(this.connection).AddArticleToFolder(TestUtilities.GetRandomArticle(), WellKnownLocalFolderIds.Unread);
        var destinationFolder = this.folders.AddKnownFolder("Sample", 1L, 1L, true);

        _ = new ArticleChanges(this.connection).CreatePendingArticleMove(sampleArticle.Id, destinationFolder.LocalId);

        Assert.Throws<FolderHasPendingArticleMoveException>(() => this.folders.DeleteFolder(destinationFolder.LocalId));
    }

    [Fact]
    public void LedgerDoesNotOperateAfterBeingDisposed()
    {
        this.ledger.Dispose();

        var folder = this.folders.CreateFolder("Something");
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());

        var knownFolder = this.folders.AddKnownFolder("Known", 1L, 1L, true);
        this.folders.DeleteFolder(knownFolder.LocalId);
        Assert.Empty(this.folderChanges.ListPendingFolderDeletes());
    }
}

public sealed class ArticleLedgerTests : IDisposable
{
    private IDbConnection connection;
    private IArticleDatabase articles;
    private IArticleChangesDatabase articleChanges;
    private DatabaseFolder CustomFolder1;
    private DatabaseFolder CustomFolder2;
    private Ledger ledger;

    public ArticleLedgerTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.articles = new ArticleDatabase(this.connection);
        this.articleChanges = new ArticleChanges(this.connection);

        var folders = new FolderDatabase(this.connection);
        this.CustomFolder1 = folders.CreateFolder("Sample1");
        this.CustomFolder2 = folders.CreateFolder("Sample2");

        this.ledger = new(folders,
                          (IArticleDatabaseWithTransactionEvents)this.articles);
    }

    public void Dispose()
    {
        this.ledger.Dispose();
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public void PendingDeleteCreatedWhenArticleDeleted()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleDeletes());
        this.articles.DeleteArticle(article.Id);

        var pendingDeletes = this.articleChanges.ListPendingArticleDeletes();
        Assert.Single(pendingDeletes);

        Assert.Equal(article.Id, pendingDeletes.First()!);
    }

    [Fact]
    public void MovingArticleToFolderCreatesPendingMove()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleMoves());
        this.articles.MoveArticleToFolder(
            article.Id,
            this.CustomFolder1.LocalId
        );

        var pendingMoves = this.articleChanges.ListPendingArticleMoves();
        Assert.Single(pendingMoves);

        Assert.Equal(article.Id, pendingMoves.First()!.ArticleId);
        Assert.Equal(this.CustomFolder1.LocalId, pendingMoves.First()!.DestinationFolderLocalId);
    }

    [Fact]
    public void MovingArticleMultipleTimesToDifferentFolderLeavesSinglePendingMove()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleMoves());
        this.articles.MoveArticleToFolder(
            article.Id,
            this.CustomFolder1.LocalId
        );

        this.articles.MoveArticleToFolder(
            article.Id,
            this.CustomFolder2.LocalId
        );

        var pendingMoves = this.articleChanges.ListPendingArticleMoves();
        Assert.Single(pendingMoves);

        Assert.Equal(article.Id, pendingMoves.First()!.ArticleId);
        Assert.Equal(this.CustomFolder2.LocalId, pendingMoves.First()!.DestinationFolderLocalId);
    }

    [Fact]
    public void LikingUnlikedArticleCreatesPendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.LikeArticle(article.Id);

        var pendingStateChanges = this.articleChanges.ListPendingArticleStateChanges();
        Assert.Single(pendingStateChanges);
        Assert.Equal(article.Id, pendingStateChanges.First()!.ArticleId);
        Assert.True(pendingStateChanges.First()!.Liked);
    }

    [Fact]
    public void LikingLikedArticleDoesNotCreatePendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle() with { liked = true },
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.LikeArticle(article.Id);

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void UnlikingLikedArticleCreatesPendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle() with { liked = true },
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.UnlikeArticle(article.Id);

        var pendingStateChanges = this.articleChanges.ListPendingArticleStateChanges();
        Assert.Single(pendingStateChanges);
        Assert.Equal(article.Id, pendingStateChanges.First()!.ArticleId);
        Assert.False(pendingStateChanges.First()!.Liked);
    }

    [Fact]
    public void UnlikingUnlikedArticleDoesNotCreatePendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.UnlikeArticle(article.Id);

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void UnlikingLikedArticleWithPendingEditRemovesPendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.LikeArticle(article.Id);
        Assert.NotEmpty(this.articleChanges.ListPendingArticleStateChanges());

        this.articles.UnlikeArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void LikingUnlikedArticleWithPendingEditRemovesPendingEdit()
    {
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle() with { liked = true },
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
        this.articles.UnlikeArticle(article.Id);
        Assert.NotEmpty(this.articleChanges.ListPendingArticleStateChanges());

        this.articles.LikeArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());
    }

    [Fact]
    public void LedgerDoesNotOperateAfterBeingDisposed()
    {
        this.ledger?.Dispose();

        var article = this.articles.AddArticleToFolder(TestUtilities.GetRandomArticle(), WellKnownLocalFolderIds.Unread);

        this.articles.LikeArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());

        this.articles.MoveArticleToFolder(article.Id, this.CustomFolder1.LocalId);
        Assert.Empty(this.articleChanges.ListPendingArticleMoves());

        this.articles.DeleteArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleDeletes());
    }
}