using System.Data;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IDisposable
{
    private IList<DatabaseArticle> SampleArticles = new List<DatabaseArticle>();
    private IList<DatabaseFolder> SampleFolders = new List<DatabaseFolder>();

    private IDbConnection connection;
    private IArticleChangesDatabase articleChangesDb;

    public ArticleChangesTests()
    {
        this.connection = TestUtilities.GetConnection();
        this.articleChangesDb = new ArticleChanges(this.connection);

        var folderDb = new FolderDatabase(this.connection);

        this.SampleArticles = new List<DatabaseArticle>() {
            this.AddRandomArticle(),
            this.AddRandomArticle(),
            this.AddRandomArticle()
        };

        for (var index = 1; index < 3; index += 1)
        {
            this.SampleFolders.Add(folderDb.CreateFolder(index.ToString()));
        }
    }

    private DatabaseArticle AddRandomArticle()
    {
        var articleDb = new ArticleDatabase(this.connection);
        var article = articleDb.AddArticleToFolder(
            TestUtilities.GetRandomArticle(),
            WellKnownLocalFolderIds.Unread
        );

        return article;
    }

    public void Dispose()
    {
        // Clean up
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public void CanGetFolderChangesDatabase()
    {
        Assert.NotNull(this.articleChangesDb);
    }

    [Fact]
    public void CanAddPendingUrlWithTitle()
    {
        var result = this.articleChangesDb.CreatePendingArticleAdd(
            TestUtilities.BASE_URI,
            TestUtilities.SAMPLE_TITLE
        );

        Assert.Equal(TestUtilities.BASE_URI, result.Url);
        Assert.Equal(TestUtilities.SAMPLE_TITLE, result.Title);
    }

    [Fact]
    public void CanAddPendingUrlWithoutTitle()
    {
        var result = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, null);
        Assert.Equal(TestUtilities.BASE_URI, result.Url);
        Assert.Null(result.Title);
    }

    [Fact]
    public void AddingArticleWithTitleExistingUrlFails()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        Assert.Throws<DuplicatePendingArticleAddException>(() => this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE));
    }

    [Fact]
    public void AddingArticleWithoutTitleExistingUrlFails()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, null);
        Assert.Throws<DuplicatePendingArticleAddException>(() => this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, null));
    }

    [Fact]
    public void AddingArticleWithDifferentTitleExistingUrlFails()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        Assert.Throws<DuplicatePendingArticleAddException>(() => this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, null));
    }

    [Fact]
    public void CanRetrieveArticleByUrlWithTitle()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        var result = this.articleChangesDb.GetPendingArticleAddByUrl(TestUtilities.BASE_URI);
        Assert.NotNull(result);
        Assert.Equal(TestUtilities.BASE_URI, result!.Url);
        Assert.Equal(TestUtilities.SAMPLE_TITLE, result!.Title);
    }

    [Fact]
    public void CanRetrieveArticleByUrlWithoutTitle()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, null);
        var result = this.articleChangesDb.GetPendingArticleAddByUrl(TestUtilities.BASE_URI);
        Assert.NotNull(result);
        Assert.Equal(TestUtilities.BASE_URI, result!.Url);
        Assert.Null(result!.Title);
    }


    [Fact]
    public void TryingToRetrieveNonExistantUrlReturnsNull()
    {
        var result = this.articleChangesDb.GetPendingArticleAddByUrl(TestUtilities.BASE_URI);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleAdds()
    {
        var articleOne = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        var articleTwo = this.articleChangesDb.CreatePendingArticleAdd(new(TestUtilities.BASE_URI, "/somethingelse"), TestUtilities.SAMPLE_TITLE);

        var result = this.articleChangesDb.ListPendingArticleAdds();
        Assert.Equal(2, result.Count);
        Assert.Contains(articleOne, result);
        Assert.Contains(articleTwo, result);
    }

    [Fact]
    public void CanDeletePendingArticleAdd()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        this.articleChangesDb.DeletePendingArticleAdd(TestUtilities.BASE_URI);

        var result = this.articleChangesDb.GetPendingArticleAddByUrl(TestUtilities.BASE_URI);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistantArticleAddSucceeds()
    {
        _ = this.articleChangesDb.CreatePendingArticleAdd(TestUtilities.BASE_URI, TestUtilities.SAMPLE_TITLE);
        this.articleChangesDb.DeletePendingArticleAdd(new(TestUtilities.BASE_URI, "/something"));

        var result = this.articleChangesDb.GetPendingArticleAddByUrl(TestUtilities.BASE_URI);
        Assert.NotNull(result);
    }

    [Fact]
    public void CanCreatePendingArticleDelete()
    {
        var sampleArticle = this.SampleArticles.First();
        var result = this.articleChangesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Equal(sampleArticle.Id, result);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleDeleteThrowsException()
    {
        var sampleArticle = this.SampleArticles.First();
        _ = this.articleChangesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Throws<DuplicatePendingArticleDeleteException>(() => this.articleChangesDb.CreatePendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CanCheckExistenceOfPendingArticleDeleteById()
    {
        var sampleArticle = this.SampleArticles.First();
        _ = this.articleChangesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.True(this.articleChangesDb.HasPendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CheckingForPendingArticleDeleteThatIsntPresentReturnsFalse()
    {
        Assert.False(this.articleChangesDb.HasPendingArticleDelete(this.SampleArticles.First().Id));
    }

    [Fact]
    public void CanListPendingArticleDeletes()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        _ = this.articleChangesDb.CreatePendingArticleDelete(firstArticleId);
        _ = this.articleChangesDb.CreatePendingArticleDelete(secondArticleId);

        var result = this.articleChangesDb.ListPendingArticleDeletes();
        Assert.Equal(2, result.Count);
        Assert.Contains(firstArticleId, result);
        Assert.Contains(secondArticleId, result);
    }

    [Fact]
    public void ListingPendingArticleDeletesWhenEmptyReturnsEmptyList()
    {
        var result = this.articleChangesDb.ListPendingArticleDeletes();
        Assert.Empty(result);
    }

    [Fact]
    public void CanDeletePendingArticleDelete()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        _ = this.articleChangesDb.CreatePendingArticleDelete(sampleArticleId);
        this.articleChangesDb.DeletePendingArticleDelete(sampleArticleId);

        Assert.False(this.articleChangesDb.HasPendingArticleDelete(sampleArticleId));
    }

    [Fact]
    public void DeletingNonExistentArticleDeleteSucceeds()
    {
        this.articleChangesDb.DeletePendingArticleDelete(this.SampleArticles.First().Id);
    }

    [Fact]
    public void CanCreatePendingArticleStateLikingChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var result = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void CanCreatePendingArticleStateUnlikingChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var result = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, false);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.False(result.Liked);
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithSameLikeStateThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        _ = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true));
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithDifferentLikeStateThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        _ = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, false));
    }

    [Fact]
    public void CanRetrievePendingArticleState()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        _ = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        var result = this.articleChangesDb.GetPendingArticleStateChangeByArticleId(sampleArticleId);
        Assert.NotNull(result);
        Assert.Equal(sampleArticleId, result!.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void RetrievingPendingArticleStateForNonExistantPendingArticleStateChangeReturnsNull()
    {
        var result = this.articleChangesDb.GetPendingArticleStateChangeByArticleId(this.SampleArticles.First().Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleStateChanges()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        _ = this.articleChangesDb.CreatePendingArticleStateChange(firstArticleId, true);
        _ = this.articleChangesDb.CreatePendingArticleStateChange(secondArticleId, false);

        var result = this.articleChangesDb.ListPendingArticleStateChanges();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.Liked);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && !x.Liked);
    }

    [Fact]
    public void CanListPendingArticleStateChangesWhenEmpty()
    {
        var result = this.articleChangesDb.ListPendingArticleStateChanges();
        Assert.Empty(result);
    }

    [Fact]
    public void CanDeletePendingArticleStateChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        _ = this.articleChangesDb.CreatePendingArticleStateChange(sampleArticleId, true);

        this.articleChangesDb.DeletePendingArticleStateChange(sampleArticleId);

        var result = this.articleChangesDb.GetPendingArticleStateChangeByArticleId(sampleArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistentPendingArticleStateChangeSucceeds()
    {
        this.articleChangesDb.DeletePendingArticleStateChange(this.SampleArticles.First().Id);
    }

    [Fact]
    public void CanCreatePendingArticleMove()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var result = this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.Equal(destinationFolderLocalId, result.DestinationFolderLocalId);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleMoveThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        _ = this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);
        Assert.Throws<DuplicatePendingArticleMoveException>(() => this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveWithNonExistentFolderAndArticleThrowsArticleNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.Last().Id + 1;
        var destinationFolderLocalId = this.SampleFolders.Last().LocalId + 1;

        Assert.Throws<ArticleNotFoundException>(() => this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveToFolderThatDoesntExistThrowsFolderNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.Last().LocalId + 1;

        Assert.Throws<FolderNotFoundException>(() => this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveToAFolderThatExistsButArticleDoesNotThrowsArticleNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.Last().Id + 1;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        Assert.Throws<ArticleNotFoundException>(() => this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CanGetPendingArticleMoveByArticleId()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        _ = this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        var result = this.articleChangesDb.GetPendingArticleMove(sampleArticleId);
        Assert.NotNull(result);
        Assert.Equal(sampleArticleId, result!.ArticleId);
        Assert.Equal(destinationFolderLocalId, result.DestinationFolderLocalId);
    }

    [Fact]
    public void RetrievingPendingArticleMoveThatDoesntExistReturnsNull()
    {
        var result = this.articleChangesDb.GetPendingArticleMove(this.SampleArticles.First().Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleMoves()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;
        _ = this.articleChangesDb.CreatePendingArticleMove(firstArticleId, destinationFolderLocalId);
        _ = this.articleChangesDb.CreatePendingArticleMove(secondArticleId, destinationFolderLocalId);

        var result = this.articleChangesDb.ListPendingArticleMoves();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
    }

    [Fact]
    public void ListingPendingArticleMovesWithNothingPendedReturnsEmpty()
    {
        var result = this.articleChangesDb.ListPendingArticleMoves();
        Assert.Empty(result);
    }

    [Fact]
    public void CanListPendingArticleMovesForSpecificFolder()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1].Id;
        var thirdArticleId = this.SampleArticles[2].Id;

        var destinationFolderLocalId = this.SampleFolders.First().LocalId;
        var secondDestinationFolder = this.SampleFolders[1].LocalId;

        _ = this.articleChangesDb.CreatePendingArticleMove(firstArticleId, destinationFolderLocalId);
        _ = this.articleChangesDb.CreatePendingArticleMove(secondArticleId, destinationFolderLocalId);
        _ = this.articleChangesDb.CreatePendingArticleMove(thirdArticleId, secondDestinationFolder);

        var result = this.articleChangesDb.ListPendingArticleMovesForLocalFolderId(destinationFolderLocalId);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
    }

    [Fact]
    public void ListingPendingArticleMovesForANonExistantFolderDoesNotThrowException()
    {
        var result = this.articleChangesDb.ListPendingArticleMovesForLocalFolderId(this.SampleFolders.Last().LocalId + 1);
        Assert.Empty(result);
    }

    [Fact]
    public void CanDeletePendingArticleMove()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        _ = this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        this.articleChangesDb.DeletePendingArticleMove(sampleArticleId);

        var result = this.articleChangesDb.GetPendingArticleMove(sampleArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistantPendingArticleMoveSucceeds()
    {
        this.articleChangesDb.DeletePendingArticleMove(this.SampleArticles.First().Id);
    }

    [Fact]
    public void DeletingFolderThatHasPendingArticleMoveToItThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        _ = this.articleChangesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        Assert.Throws<InvalidOperationException>(() => new FolderDatabase(this.connection).DeleteFolder(destinationFolderLocalId));
    }
}