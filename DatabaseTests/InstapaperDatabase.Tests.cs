using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public sealed class InstapaperDatabaseTests
{
    [Fact]
    public void CanReopenDatabase()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        InstapaperDatabase.OpenOrCreateDatabase(connection);

        connection.Close();

        InstapaperDatabase.OpenOrCreateDatabase(connection);
    }
}