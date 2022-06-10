using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
    private static readonly Uri SAMPLE_URL = new("https://www.codevoid.net");
    private const string SAMPLE_TITLE = "Codevoid";

    private IInstapaperDatabase? db;

    public async Task InitializeAsync()
    {
        this.db = await TestUtilities.GetDatabase();
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
        Assert.Throws<DuplicatePendingArticleAdd>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE));
    }

    [Fact]
    public void AddingArticleWithoutTitleExistingUrlFails()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, null);
        Assert.Throws<DuplicatePendingArticleAdd>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, null));
    }

    [Fact]
    public void AddingArticleWithDifferentTitleExistingUrlFails()
    {
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleAdd(SAMPLE_URL, SAMPLE_TITLE);
        Assert.Throws<DuplicatePendingArticleAdd>(() => changesDb.CreatePendingArticleAdd(SAMPLE_URL, null));
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
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        var result = changesDb.CreatePendingArticleDelete(ARTICLE_ID);
        Assert.Equal(ARTICLE_ID, result);
    }

    [Fact]
    public void CreatingDuplicatePendingArticleDeleteThrowsException()
    {
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(ARTICLE_ID);
        Assert.Throws<DuplicatePendingArticleDelete>(() => changesDb.CreatePendingArticleDelete(ARTICLE_ID));
    }

    [Fact]
    public void CanCheckExistenceOfPendingArticleDeleteById()
    {
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(ARTICLE_ID);
        Assert.True(changesDb.HasPendingArticleDelete(ARTICLE_ID));
    }

    [Fact]
    public void CheckingForPendingArticleDeleteThatIsntPresentReturnsFalse()
    {
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        Assert.False(changesDb.HasPendingArticleDelete(ARTICLE_ID));
    }

    [Fact]
    public void CanListPendingArticleDeletes()
    {
        const long ARTICLE_ID_1 = 42L;
        const long ARTICLE_ID_2 = 43L;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(ARTICLE_ID_1);
        _ = changesDb.CreatePendingArticleDelete(ARTICLE_ID_2);

        var result = changesDb.ListPendingArticleDeletes();
        Assert.Equal(2, result.Count);
        Assert.Contains(ARTICLE_ID_1, result);
        Assert.Contains(ARTICLE_ID_2, result);
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
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        _ = changesDb.CreatePendingArticleDelete(ARTICLE_ID);
        changesDb.RemovePendingArticleDelete(ARTICLE_ID);

        Assert.False(changesDb.HasPendingArticleDelete(ARTICLE_ID));
    }

    [Fact]
    public void DeletingNonExistentArticleDeleteSucceeds()
    {
        const long ARTICLE_ID = 42L;
        var changesDb = this.db!.ArticleChangesDatabase;
        changesDb.RemovePendingArticleDelete(ARTICLE_ID);
    }
}