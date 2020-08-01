using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Codevoid.Test.Storyvoid
{
    public sealed class ArticleDatabaseTests
    {
        [Fact]
        public async Task CanOpenDatabase()
        {
            using var db = await TestUtilities.GetDatabase();
            Assert.NotNull(db);
        }

        [Fact]
        public async Task CanReopenDatabase()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            var first = new ArticleDatabase(connection);
            await first.OpenOrCreateDatabaseAsync();

            var second = new ArticleDatabase(connection);
            await first.OpenOrCreateDatabaseAsync();
        }
    }
}
