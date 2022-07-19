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
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateDatabaseIfNeeded();

        return connection;
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

/// <summary>
/// Utility class to hold event source + sink for database changes.
/// </summary>
internal sealed class DatabaseEventClearingHouse : IDatabaseEventSink, IDatabaseEventSource
{
    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderAdded;

    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderDeleted;

    /// <inheritdoc />
    public event EventHandler<DatabaseFolder>? FolderUpdated;

    /// <inheritdoc />
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleAdded;

    /// <inheritdoc />
    public event EventHandler<long>? ArticleDeleted;

    /// <inheritdoc />
    public event EventHandler<(DatabaseArticle Article, long LocalFolderId)>? ArticleMoved;

    /// <inheritdoc />
    public event EventHandler<DatabaseArticle>? ArticleUpdated;

    /// <inheritdoc />
    public void RaiseFolderAdded(DatabaseFolder added)
    {
        var handler = this.FolderAdded;
        handler?.Invoke(this, added);
    }

    /// <inheritdoc />
    public void RaiseFolderDeleted(DatabaseFolder deleted)
    {
        var handler = this.FolderDeleted;
        handler?.Invoke(this, deleted);
    }

    /// <inheritdoc />
    public void RaiseFolderUpdated(DatabaseFolder updated)
    {
        var handler = this.FolderUpdated;
        handler?.Invoke(this, updated);
    }

    /// <inheritdoc />
    public void RaiseArticleAdded(DatabaseArticle added, long toLocalId)
    {
        var handler = this.ArticleAdded;
        handler?.Invoke(this, (added, toLocalId));
    }

    /// <inheritdoc />
    public void RaiseArticleDeleted(long articleId)
    {
        var handler = this.ArticleDeleted;
        handler?.Invoke(this, articleId);
    }

    /// <inheritdoc />
    public void RaiseArticleMoved(DatabaseArticle article, long toLocalId)
    {
        var handler = this.ArticleMoved;
        handler?.Invoke(this, (article, toLocalId));
    }

    /// <inheritdoc />
    public void RaiseArticleUpdated(DatabaseArticle updated)
    {
        var handler = this.ArticleUpdated;
        handler?.Invoke(this, updated);
    }
}