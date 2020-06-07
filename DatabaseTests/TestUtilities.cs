using System;
using System.Threading.Tasks;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid
{
    public static class TestUtilities
    {
        internal static async Task<IArticleDatabase> GetDatabase()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var db = new ArticleDatabase(connection);
            await db.OpenOrCreateDatabaseAsync();

            return db;
        }
    }
}
