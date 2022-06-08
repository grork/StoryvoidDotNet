using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

/// <inheritdoc />
public class ArticleChanges : IArticleChangesDatabase
{
    private IDbConnection connection;

    private ArticleChanges(IDbConnection connection)
    {
        this.connection = connection;
    }

    /// <inheritdoc />
    public long CreatePendingArticleAdd(Uri url, string? title)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
            INSERT INTO article_adds(url, title)
            VALUES (@url, @title);

            SELECT last_insert_rowid();
        ");

        query.AddParameter("@url", url);
        if (title is null)
        {
            query.AddNull("@title", DbType.String);
        }
        else
        {
            query.AddParameter("@title", title);
        }

        try
        {
            return (long)query.ExecuteScalar();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_UNIQUE)
        {
            throw new DuplicatePendingArticleAdd(url);
        }
    }

    /// <summary>
    /// For the supplied DB connection, get an instance of the Pending Article
    /// Changes API.
    /// </summary>
    /// <param name="connection">
    /// The opened DB Connection to use to access the database.
    /// </param>
    /// <returns>Instance of the the API</returns>
    public static IArticleChangesDatabase GetPendingArticleChangeDatabase(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Database must be opened");
        }

        return new ArticleChanges(connection);
    }
}