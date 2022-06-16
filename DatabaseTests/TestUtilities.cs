using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public static class TestUtilities
{
    private static long nextArticleId = 1L;
    public static readonly Uri BASE_URI = new("https://www.codevoid.net");
    public const string SAMPLE_TITLE = "Codevoid";

    internal static async Task<IInstapaperDatabase> GetDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        var db = new InstapaperDatabase(connection);
        return await db.OpenOrCreateDatabaseAsync();
    }

    public static ArticleRecordInformation GetRandomArticle()
    {
        return new(
            id: nextArticleId++,
            title: "Sample Article",
            url: new Uri(BASE_URI, $"/{nextArticleId}"),
            description: String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "ABC",
            liked: false
        );
    }
}