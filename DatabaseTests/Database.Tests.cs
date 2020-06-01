using System;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class DatabaseTests
    {
        private static IInstapaperDatabase GetDatabase()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            return new Database(connection);
        }

        [Fact]
        public async Task CanOpenDatabase()
        {
            using var db = GetDatabase();
            await db.OpenOrCreateDatabaseAsync();
        }

        [Fact]
        public async Task CanReopenDatabase()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            var first = new Database(connection);
            await first.OpenOrCreateDatabaseAsync();

            var second = new Database(connection);
            await first.OpenOrCreateDatabaseAsync();
        }
    }
}
