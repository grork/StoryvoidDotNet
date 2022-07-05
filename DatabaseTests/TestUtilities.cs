using System.Data;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public static class TestUtilities
{
    private static long nextArticleId = 1L;
    public static readonly Uri BASE_URI = new("https://www.codevoid.net");
    public const string SAMPLE_TITLE = "Codevoid";

    internal static IDbConnection GetConnection()
    {
        var dbName = Guid.NewGuid().ToString();
        var connection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        InstapaperDatabase.CreateDatabaseIfNeeded(connection);

        return connection;
    }

    internal static Func<IDbConnection> GetFactory(this IDbConnection instance)
    {
        var connectionString = instance.ConnectionString;
        return () => new SqliteConnection(connectionString);
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