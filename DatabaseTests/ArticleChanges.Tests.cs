using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
    private static readonly Uri SAMPLE_URL = new("https://www.codevoid.net");
    private long SampleId = 41L;
    private const string SAMPLE_TITLE = "Codevoid";
    private IList<DatabaseArticle> AddedArticles = new List<DatabaseArticle>();

    private IInstapaperDatabase? db;

    public async Task InitializeAsync()
    {
        this.db = await TestUtilities.GetDatabase();
        this.AddedArticles = new List<DatabaseArticle>() {
            this.AddRandomArticle(),
            this.AddRandomArticle(),
            this.AddRandomArticle()
        };
    }

    private DatabaseArticle AddRandomArticle()
    {
        var article = this.db!.ArticleDatabase.AddArticleToFolder(new(
            id: (this.SampleId += 1),
            title: SAMPLE_TITLE,
            url: new (SAMPLE_URL, $"/{this.SampleId}"),
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
        changesDb.RemovePendingArticleAdd(SAMPLE_URL);

        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistantArticleAddSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        changesDb.RemovePendingArticleAdd(new("https://www.codevoid.net/something"));

        var result = changesDb.GetPendingArticleAddByUrl(SAMPLE_URL);
        Assert.NotNull(result);
    }

    [Fact]
    public void CanCreatePendingArticleDelete()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleDelete(this.SampleId);
        Assert.Equal(this.SampleId, result);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleDeleteThrowsException()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(this.SampleId);
        Assert.Throws<DuplicatePendingArticleDeleteException>(() => changesDb.CreatePendingArticleDelete(this.SampleId));
    }

    [Fact]
    public void CanCheckExistenceOfPendingArticleDeleteById()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(SampleId);
        Assert.True(changesDb.HasPendingArticleDelete(SampleId));
    }

    [Fact]
    public void CheckingForPendingArticleDeleteThatIsntPresentReturnsFalse()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.False(changesDb.HasPendingArticleDelete(SampleId));
    }

    [Fact]
    public void CanListPendingArticleDeletes()
    {
        var secondArticleId = this.AddedArticles[1]!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(SampleId);
        _ = changesDb.CreatePendingArticleDelete(secondArticleId);

        var result = changesDb.ListPendingArticleDeletes();
        Assert.Equal(2, result.Count);
        Assert.Contains(SampleId, result);
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
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(SampleId);
        changesDb.RemovePendingArticleDelete(SampleId);

        Assert.False(changesDb.HasPendingArticleDelete(SampleId));
    }

    [Fact]
    public void DeletingNonExistentArticleDeleteSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.RemovePendingArticleDelete(SampleId);
    }

    [Fact]
    public void CanCreatePendingArticleStateLikingChange()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(SampleId, true);
        Assert.Equal(SampleId, result.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void CanCreatePendingArticleStateUnlikingChange()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(SampleId, false);
        Assert.Equal(SampleId, result.ArticleId);
        Assert.False(result.Liked);
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithSameLikeStateThrowsException()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(SampleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(SampleId, true));
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithDifferentLikeStateThrowsException()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(SampleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(SampleId, false));
    }

    [Fact]
    public void CanRetrievePendingArticleState()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(SampleId, true);
        var result = changesDb.GetPendingArticleStateChangeByArticleId(SampleId);
        Assert.NotNull(result);
        Assert.Equal(SampleId, result!.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void RetrievingPendingArticleStateForNonExistantPendingArticleStateChangeReturnsNull()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.GetPendingArticleStateChangeByArticleId(SampleId);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleStateChanges()
    {
        var secondArticleId = this.AddedArticles[1]!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(SampleId, true);
        _ = changesDb.CreatePendingArticleStateChange(secondArticleId, false);

        var result = changesDb.ListPendingArticleStateChanges();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ArticleId == this.SampleId && x.Liked);
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
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(SampleId, true);

        changesDb.RemovePendingArticleStateChange(SampleId);

        var result = changesDb.GetPendingArticleStateChangeByArticleId(SampleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistentPendingArticleStateChangeSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.RemovePendingArticleStateChange(SampleId);
    }
}