using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class FolderLedgerTests : IDisposable
{
    private IInstapaperDatabase db;
    private IFolderDatabase folders;
    private IFolderChangesDatabase folderChanges;
    private Ledger ledger;

    public FolderLedgerTests()
    {
        this.db = TestUtilities.GetDatabase();
        this.folders = this.db.FolderDatabase;
        this.folderChanges = this.db.FolderChangesDatabase;
        this.ledger = new((IFolderDatabaseWithTransactionEvents)this.folders,
                          this.folderChanges,
                          (IArticleDatabaseWithTransactionEvents)this.db.ArticleDatabase,
                          this.db.ArticleChangesDatabase);
    }

    public void Dispose()
    {
        this.ledger.Dispose();
        this.db.Dispose();
    }

    [Fact]
    public void AddsPendingAddWhenFolderIsCreated()
    {
        Assert.Empty(this.folderChanges.ListPendingFolderAdds());

        var folder = this.folders.CreateFolder("Sample");
        var pendingAdds = this.folderChanges.ListPendingFolderAdds();
        Assert.Equal(1, pendingAdds.Count);
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
        Assert.Equal(1, pendingDeletes.Count);
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
        Assert.Equal(1, this.folderChanges.ListPendingFolderDeletes().Count);
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
        var sampleArticle = this.db.ArticleDatabase.AddArticleToFolder(TestUtilities.GetRandomArticle(), WellKnownLocalFolderIds.Unread);
        var destinationFolder = this.folders.AddKnownFolder("Sample", 1L, 1L, true);

        var changesDb = this.db.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticle.Id, destinationFolder.LocalId);

        Assert.Throws<FolderHasPendingArticleMoveException>(() => this.db.FolderDatabase.DeleteFolder(destinationFolder.LocalId));
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
    private IInstapaperDatabase db;
    private IArticleDatabase articles;
    private IArticleChangesDatabase articleChanges;
    private IFolderDatabase folders;
    private Ledger ledger;

    public ArticleLedgerTests()
    {
        this.db = TestUtilities.GetDatabase();
        this.articles = this.db.ArticleDatabase;
        this.articleChanges = this.db.ArticleChangesDatabase;
        this.folders = this.db.FolderDatabase;
        this.ledger = new((IFolderDatabaseWithTransactionEvents)this.folders,
                          this.db.FolderChangesDatabase,
                          (IArticleDatabaseWithTransactionEvents)this.articles,
                          this.articleChanges);
    }

    public void Dispose()
    {
        this.ledger.Dispose();
        this.db.Dispose();
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
        Assert.Equal(1, pendingDeletes.Count);

        Assert.Equal(article.Id, pendingDeletes[0]);
    }

    [Fact]
    public void MovingArticleToFolderCreatesPendingMove()
    {
        var folder = this.folders.CreateFolder("Sample");
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleMoves());
        this.articles.MoveArticleToFolder(
            article.Id,
            folder.LocalId
        );

        var pendingMoves = this.articleChanges.ListPendingArticleMoves();
        Assert.Equal(1, pendingMoves.Count);

        Assert.Equal(article.Id, pendingMoves[0].ArticleId);
        Assert.Equal(folder.LocalId, pendingMoves[0].DestinationFolderLocalId);
    }

    [Fact]
    public void MovingArticleMultipleTimesToDifferentFolderLeavesSinglePendingMove()
    {
        var folder1 = this.folders.CreateFolder("Sample");
        var folder2 = this.folders.CreateFolder("Sample 2");
        var article = this.articles.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        Assert.Empty(this.articleChanges.ListPendingArticleMoves());
        this.articles.MoveArticleToFolder(
            article.Id,
            folder1.LocalId
        );

        this.articles.MoveArticleToFolder(
            article.Id,
            folder2.LocalId
        );

        var pendingMoves = this.articleChanges.ListPendingArticleMoves();
        Assert.Equal(1, pendingMoves.Count);

        Assert.Equal(article.Id, pendingMoves[0].ArticleId);
        Assert.Equal(folder2.LocalId, pendingMoves[0].DestinationFolderLocalId);
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
        Assert.Equal(1, pendingStateChanges.Count);
        Assert.Equal(article.Id, pendingStateChanges[0].ArticleId);
        Assert.True(pendingStateChanges[0].Liked);
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
        Assert.Equal(1, pendingStateChanges.Count);
        Assert.Equal(article.Id, pendingStateChanges[0].ArticleId);
        Assert.False(pendingStateChanges[0].Liked);
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

        var folder = this.folders.CreateFolder("Sample");
        var article = this.articles.AddArticleToFolder(TestUtilities.GetRandomArticle(), WellKnownLocalFolderIds.Unread);

        this.articles.LikeArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleStateChanges());

        this.articles.MoveArticleToFolder(article.Id, folder.LocalId);
        Assert.Empty(this.articleChanges.ListPendingArticleMoves());

        this.articles.DeleteArticle(article.Id);
        Assert.Empty(this.articleChanges.ListPendingArticleDeletes());
    }
}