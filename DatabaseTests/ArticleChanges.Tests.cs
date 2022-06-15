using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
    private static readonly Uri SAMPLE_URL = new("https://www.codevoid.net");
    private long nextArticleId = 41L;
    private const string SAMPLE_TITLE = "Codevoid";
    private IList<DatabaseArticle> SampleArticles = new List<DatabaseArticle>();
    private IList<DatabaseFolder> SampleFolders = new List<DatabaseFolder>();

    private IInstapaperDatabase? db;

    public async Task InitializeAsync()
    {
        this.db = await TestUtilities.GetDatabase();
        this.SampleArticles = new List<DatabaseArticle>() {
            this.AddRandomArticle(),
            this.AddRandomArticle(),
            this.AddRandomArticle()
        };

        for (var index = 1; index < 3; index +=1)
        {
            this.SampleFolders.Add(this.db!.FolderDatabase.CreateFolder(index.ToString()));
        }
    }

    private DatabaseArticle AddRandomArticle()
    {
        var article = this.db!.ArticleDatabase.AddArticleToFolder(new(
            id: (this.nextArticleId += 1),
            title: SAMPLE_TITLE,
            url: new (SAMPLE_URL, $"/{this.nextArticleId}"),
            description: String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "ABC",
            liked: false
        ), WellKnownLocalFolderIds.Unread);

        return article;
    }

    public Task DisposeAsync()
    {
        this.db?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void CanGetFolderChangesDatabase()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.NotNull(changesDb);
    }

    [Fact]
    public void CanAddPendingUrlWithTitle()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        Assert.Equal(SAMPLE_URL, result.Url);
        Assert.Equal(SAMPLE_TITLE, result.Title);
    }

    [Fact]
    public void CanAddPendingUrlWithoutTitle()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleAdd(SAMPLE_URL, null);
        Assert.Equal(SAMPLE_URL, result.Url);
        Assert.Null(result.Title);
    }

    [Fact]
    public void AddingArticleWithTitleExistingUrlFails()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        Assert.Throws<DuplicatePendingArticleAddException>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE));
    }

    [Fact]
    public void AddingArticleWithoutTitleExistingUrlFails()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, null);
        Assert.Throws<DuplicatePendingArticleAddException>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, null));
    }

    [Fact]
    public void AddingArticleWithDifferentTitleExistingUrlFails()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        Assert.Throws<DuplicatePendingArticleAddException>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, null));
    }

    [Fact]
    public void CanRetrieveArticleByUrlWithTitle()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.NotNull(result);
        Assert.Equal(SAMPLE_URL, result!.Url);
        Assert.Equal(SAMPLE_TITLE, result!.Title);
    }

    [Fact]
    public void CanRetrieveArticleByUrlWithoutTitle()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, null);
        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.NotNull(result);
        Assert.Equal(SAMPLE_URL, result!.Url);
        Assert.Null(result!.Title);
    }


    [Fact]
    public void TryingToRetrieveNonExistantUrlReturnsNull()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleAdds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var articleOne = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        var articleTwo = changesDb.CreatePendingArticleAdd(new (SAMPLE_URL, "/somethingelse"), SAMPLE_TITLE);

        var result = changesDb.ListPendingArticleAdds();
        Assert.Equal(2, result.Count);
        Assert.Contains(articleOne, result);
        Assert.Contains(articleTwo, result);
    }

    [Fact]
    public void CanDeletePendingArticleAdd()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        changesDb.DeletePendingArticleAdd(SAMPLE_URL);

        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistantArticleAddSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        changesDb.DeletePendingArticleAdd(new("https://www.codevoid.net/something"));

        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.NotNull(result);
    }

    [Fact]
    public void CanCreatePendingArticleDelete()
    {
        var sampleArticle = this.SampleArticles.First();
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Equal(sampleArticle.Id, result);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleDeleteThrowsException()
    {
        var sampleArticle = this.SampleArticles.First();
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Throws<DuplicatePendingArticleDeleteException>(() => changesDb.CreatePendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CanCheckExistenceOfPendingArticleDeleteById()
    {
        var sampleArticle = this.SampleArticles.First();
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.True(changesDb.HasPendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CheckingForPendingArticleDeleteThatIsntPresentReturnsFalse()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.False(changesDb.HasPendingArticleDelete(this.SampleArticles.First().Id));
    }

    [Fact]
    public void CanListPendingArticleDeletes()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(firstArticleId);
        _ = changesDb.CreatePendingArticleDelete(secondArticleId);

        var result = changesDb.ListPendingArticleDeletes();
        Assert.Equal(2, result.Count);
        Assert.Contains(firstArticleId, result);
        Assert.Contains(secondArticleId, result);
    }

    [Fact]
    public void ListingPendingArticleDeletesWhenEmptyReturnsEmptyList()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.ListPendingArticleDeletes();
        Assert.Empty(result);
    }

    [Fact]
    public void CanDeletePendingArticleDelete()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticleId);
        changesDb.DeletePendingArticleDelete(sampleArticleId);

        Assert.False(changesDb.HasPendingArticleDelete(sampleArticleId));
    }

    [Fact]
    public void DeletingNonExistentArticleDeleteSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.DeletePendingArticleDelete(this.SampleArticles.First().Id);
    }

    [Fact]
    public void CanCreatePendingArticleStateLikingChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void CanCreatePendingArticleStateUnlikingChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(sampleArticleId, false);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.False(result.Liked);
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithSameLikeStateThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(sampleArticleId, true));
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithDifferentLikeStateThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(sampleArticleId, false));
    }

    [Fact]
    public void CanRetrievePendingArticleState()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        var result = changesDb.GetPendingArticleStateChangeByArticleId(sampleArticleId);
        Assert.NotNull(result);
        Assert.Equal(sampleArticleId, result!.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void RetrievingPendingArticleStateForNonExistantPendingArticleStateChangeReturnsNull()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.GetPendingArticleStateChangeByArticleId(this.SampleArticles.First().Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleStateChanges()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(firstArticleId, true);
        _ = changesDb.CreatePendingArticleStateChange(secondArticleId, false);

        var result = changesDb.ListPendingArticleStateChanges();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.Liked);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && !x.Liked);
    }

    [Fact]
    public void CanListPendingArticleStateChangesWhenEmpty()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.ListPendingArticleStateChanges();
        Assert.Empty(result);
    }

    [Fact]
    public void CanDeletePendingArticleStateChange()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);

        changesDb.DeletePendingArticleStateChange(sampleArticleId);

        var result = changesDb.GetPendingArticleStateChangeByArticleId(sampleArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistentPendingArticleStateChangeSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.DeletePendingArticleStateChange(this.SampleArticles.First().Id);
    }

    [Fact]
    public void CanCreatePendingArticleMove()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.Equal(destinationFolderLocalId, result.DestinationFolderLocalId);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleMoveThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);
        Assert.Throws<DuplicatePendingArticleMoveException>(() => changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveWithNonExistentFolderAndArticleThrowsArticleNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.Last().Id + 1;
        var destinationFolderLocalId = this.SampleFolders.Last().LocalId + 1;

        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.Throws<ArticleNotFoundException>(() => changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveToFolderThatDoesntExistThrowsFolderNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.Last().LocalId + 1;

        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.Throws<FolderNotFoundException>(() => changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CreatingAMoveToAFolderThatExistsButArticleDoesNotThrowsArticleNotFoundException()
    {
        var sampleArticleId = this.SampleArticles.Last().Id + 1;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.Throws<ArticleNotFoundException>(() => changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId));
    }

    [Fact]
    public void CanGetPendingArticleMoveByArticleId()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        var result = changesDb.GetPendingArticleMove(sampleArticleId);
        Assert.NotNull(result);
        Assert.Equal(sampleArticleId, result!.ArticleId);
        Assert.Equal(destinationFolderLocalId, result.DestinationFolderLocalId);
    }

    [Fact]
    public void RetrievingPendingArticleMoveThatDoesntExistReturnsNull()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.GetPendingArticleMove(this.SampleArticles.First().Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleMoves()
    {
        var firstArticleId = this.SampleArticles.First().Id;
        var secondArticleId = this.SampleArticles[1]!.Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(firstArticleId, destinationFolderLocalId);
        _ = changesDb.CreatePendingArticleMove(secondArticleId, destinationFolderLocalId);

        var result = changesDb.ListPendingArticleMoves();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
    }

    [Fact]
    public void ListingPendingArticleMovesWithNothingPendedReturnsEmpty()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.ListPendingArticleMoves();
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

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(firstArticleId, destinationFolderLocalId);
        _ = changesDb.CreatePendingArticleMove(secondArticleId, destinationFolderLocalId);
        _ = changesDb.CreatePendingArticleMove(thirdArticleId, secondDestinationFolder);

        var result = changesDb.ListPendingArticleMovesForLocalFolderId(destinationFolderLocalId);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == firstArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
        Assert.Contains(result, x => x.ArticleId == secondArticleId && x.DestinationFolderLocalId == destinationFolderLocalId);
    }

    [Fact]
    public void ListingPendingArticleMovesForANonExistantFolderThrowsException()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.Throws<FolderNotFoundException>(() => changesDb.ListPendingArticleMovesForLocalFolderId(this.SampleFolders.Last().LocalId + 1));
    }

    [Fact]
    public void CanDeletePendingArticleMove()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        changesDb.DeletePendingArticleMove(sampleArticleId);
        
        var result = changesDb.GetPendingArticleMove(sampleArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistantPendingArticleMoveSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.DeletePendingArticleMove(this.SampleArticles.First().Id);
    }

    [Fact]
    public void DeletingFolderThatHasPendingArticleMoveToItThrowsException()
    {
        var sampleArticleId = this.SampleArticles.First().Id;
        var destinationFolderLocalId = this.SampleFolders.First().LocalId;

        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleMove(sampleArticleId, destinationFolderLocalId);

        Assert.Throws<FolderHasPendingArticleMoveException>(() => this.db!.FolderDatabase.DeleteFolder(destinationFolderLocalId));
    }
}