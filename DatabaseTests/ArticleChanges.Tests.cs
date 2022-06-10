using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
    private static readonly Uri SAMPLE_URL = new("https://www.codevoid.net");
    private long nextArticleId = 41L;
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
        var sampleArticle = this.AddedArticles.First()!;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Equal(sampleArticle.Id, result);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleDeleteThrowsException()
    {
        var sampleArticle = this.AddedArticles.First()!;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.Throws<DuplicatePendingArticleDeleteException>(() => changesDb.CreatePendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CanCheckExistenceOfPendingArticleDeleteById()
    {
        var sampleArticle = this.AddedArticles.First()!;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticle.Id);
        Assert.True(changesDb.HasPendingArticleDelete(sampleArticle.Id));
    }

    [Fact]
    public void CheckingForPendingArticleDeleteThatIsntPresentReturnsFalse()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.False(changesDb.HasPendingArticleDelete(this.AddedArticles.First()!.Id));
    }

    [Fact]
    public void CanListPendingArticleDeletes()
    {
        var firstArticleId = this.AddedArticles.First()!.Id;
        var secondArticleId = this.AddedArticles[1]!.Id;
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
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(sampleArticleId);
        changesDb.RemovePendingArticleDelete(sampleArticleId);

        Assert.False(changesDb.HasPendingArticleDelete(sampleArticleId));
    }

    [Fact]
    public void DeletingNonExistentArticleDeleteSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.RemovePendingArticleDelete(this.AddedArticles.First()!.Id);
    }

    [Fact]
    public void CanCreatePendingArticleStateLikingChange()
    {
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.True(result.Liked);
    }

    [Fact]
    public void CanCreatePendingArticleStateUnlikingChange()
    {
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleStateChange(sampleArticleId, false);
        Assert.Equal(sampleArticleId, result.ArticleId);
        Assert.False(result.Liked);
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithSameLikeStateThrowsException()
    {
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(sampleArticleId, true));
    }

    [Fact]
    public void AddingDuplicatePendingArticleStateChangeWithDifferentLikeStateThrowsException()
    {
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);
        Assert.Throws<DuplicatePendingArticleStateChangeException>(() => changesDb.CreatePendingArticleStateChange(sampleArticleId, false));
    }

    [Fact]
    public void CanRetrievePendingArticleState()
    {
        var sampleArticleId = this.AddedArticles.First()!.Id;
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
        var result = changesDb.GetPendingArticleStateChangeByArticleId(this.AddedArticles.First()!.Id);
        Assert.Null(result);
    }

    [Fact]
    public void CanListPendingArticleStateChanges()
    {
        var firstArticleId = this.AddedArticles.First()!.Id;
        var secondArticleId = this.AddedArticles[1]!.Id;
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
        var sampleArticleId = this.AddedArticles.First()!.Id;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleStateChange(sampleArticleId, true);

        changesDb.RemovePendingArticleStateChange(sampleArticleId);

        var result = changesDb.GetPendingArticleStateChangeByArticleId(sampleArticleId);
        Assert.Null(result);
    }

    [Fact]
    public void DeletingNonExistentPendingArticleStateChangeSucceeds()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.RemovePendingArticleStateChange(this.AddedArticles.First()!.Id);
    }
}