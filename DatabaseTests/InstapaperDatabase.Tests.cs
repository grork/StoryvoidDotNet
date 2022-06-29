using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public sealed class InstapaperDatabaseTests
{
    [Fact]
    public void CanOpenDatabase()
    {
        using var db = TestUtilities.GetDatabase();
        Assert.NotNull(db);
    }

    [Fact]
    public void CanReopenDatabase()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var first = new InstapaperDatabase(connection);
        first.OpenOrCreateDatabase();

        var second = new InstapaperDatabase(connection);
        second.OpenOrCreateDatabase();
    }
}