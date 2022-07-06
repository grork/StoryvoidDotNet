using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class SyncTests
{
    [Fact]
    public void CanInstantiate()
    {
        var instance = new Sync();
        Assert.NotNull(instance);
    }
}
