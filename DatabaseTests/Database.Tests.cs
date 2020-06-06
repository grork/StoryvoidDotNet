using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public class DatabaseTests
    {
        private static async Task<IInstapaperDatabase> GetDatabase()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var db = new Database(connection);
            await db.OpenOrCreateDatabaseAsync();

            return db;
        }

        [Fact]
        public async Task CanOpenDatabase()
        {
            using var db = await GetDatabase();
            Assert.NotNull(db);
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

        [Fact]
        public async Task DefaultFoldersAreCreated()
        {
            using var db = await GetDatabase();
            IList<Object> result = await db.GetFoldersAsync();
            Assert.Equal(2, result.Count);
        }
    }
}
