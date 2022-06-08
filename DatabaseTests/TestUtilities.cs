using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public static class TestUtilities
{
    internal static async Task<IInstapaperDatabase> GetDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        var db = new InstapaperDatabase(connection);
        return await db.OpenOrCreateDatabaseAsync();
    }
}