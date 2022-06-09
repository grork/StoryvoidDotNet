using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
    private static readonly Uri SAMPLE_URL = new Uri("https://www.codevoid.net");
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
}