using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public sealed class InstapaperDatabaseTests
{
    [Fact]
    public void CanReopenDatabase()
    {
        const string CONNECTION_STRING = "Data Source=StaysInMemory;Mode=Memory;Cache=Shared";
        // To keep data in an in-memory DB, we have to maintain at least _one_
        // shared cache connection to that db.
        using var baseConnection = new SqliteConnection(CONNECTION_STRING);
        baseConnection.Open();

        // Open the DB, and put _something_ in it
        using (var connection1 = new SqliteConnection(CONNECTION_STRING))
        {
            InstapaperDatabase.OpenOrCreateDatabase(connection1);

            // Why not use the data access layer? To keep the tests decoupled.
            // This does run the risk of atrophy over time, but removing the
            // coupling seems worthy of that risk
            using var query = connection1.CreateCommand(@"
                INSERT INTO folders(title, local_id)
                VALUES ('Sample', -1);
            ");

            query.ExecuteScalar();

            connection1.Close();
        }

        // Reopen the db and get _something_ out of it
        using (var connection2 = new SqliteConnection(CONNECTION_STRING))
        {
            InstapaperDatabase.OpenOrCreateDatabase(connection2);
            using var query = connection2.CreateCommand(@"
                SELECT COUNT(local_id) FROM folders
                WHERE local_id = -1
            ");

            var count = (long)(query.ExecuteScalar()!);
            Assert.Equal(1, count);

            connection2.Close();
        }

        baseConnection.Close();
    }
}