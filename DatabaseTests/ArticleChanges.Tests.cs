using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public sealed class ArticleChangesTests : IAsyncLifetime
{
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
}