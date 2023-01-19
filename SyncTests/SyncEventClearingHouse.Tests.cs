namespace Codevoid.Test.Storyvoid.Sync;

public sealed class SyncEventClearingHouseTests
{
    private SyncEventClearingHouse clearingHouse = new SyncEventClearingHouse();

    [Fact]
    public void CanRaiseSyncStarted()
    {
        var wasRaised = false;
        this.clearingHouse.SyncStarted += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseSyncStarted();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseFoldersStarted()
    {
        var wasRaised = false;
        this.clearingHouse.FoldersStarted += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseFoldersStarted();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseFoldersEnded()
    {
        var wasRaised = false;
        this.clearingHouse.FoldersEnded += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseFoldersEnded();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseFoldersError()
    {
        var wasRaised = false;
        this.clearingHouse.FoldersError += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseFoldersError();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseArticlesStarted()
    {
        var wasRaised = false;
        this.clearingHouse.ArticlesStarted += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseArticlesStarted();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseArticlesEnded()
    {
        var wasRaised = false;
        this.clearingHouse.ArticlesEnded += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseArticlesEnded();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseArticlesError()
    {
        var wasRaised = false;
        this.clearingHouse.ArticlesError += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseArticlesError();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseSyncEnded()
    {
        var wasRaised = false;
        this.clearingHouse.SyncEnded += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseSyncEnded();
        Assert.True(wasRaised);
    }

    [Fact]
    public void CanRaiseSyncError()
    {
        var wasRaised = false;
        this.clearingHouse.SyncError += (_, _) => wasRaised = true;
        this.clearingHouse.RaiseSyncError();
        Assert.True(wasRaised);
    }
}