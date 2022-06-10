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
    public PendingArticleAdd CreatePendingArticleAdd(Uri url, string? title)
    {
        var c = this.connection;

        using var query = c.CreateCommand(@"
            INSERT INTO article_adds(url, title)
            VALUES (@url, @title);
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
            query.ExecuteScalar();
            return GetPendingArticleAddByUrl(c, url)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new DuplicatePendingArticleAdd(url);
        }
    }

    /// <inheritdoc />
    public PendingArticleAdd? GetPendingArticleAddByUrl(Uri url)
    {
        var c = this.connection;
        return GetPendingArticleAddByUrl(c, url);
    }

    /// <inheritdoc />
    public IList<PendingArticleAdd> ListPendingArticleAdds()
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            SELECT * FROM article_adds;
        ");

        using var pendingArticleAdds = query.ExecuteReader();

        var result = new List<PendingArticleAdd>();
        while(pendingArticleAdds.Read())
        {
            var pendingArticleAdd = PendingArticleAdd.FromRow(pendingArticleAdds);
            result.Add(pendingArticleAdd);
        }

        return result;
    }

    /// <inheritdoc />
    public void RemovePendingArticleAdd(Uri url)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            DELETE FROM article_adds
            WHERE url = @url
        ");

        query.AddParameter("@url", url);

        query.ExecuteNonQuery();
    }

    private static PendingArticleAdd? GetPendingArticleAddByUrl(IDbConnection connection, Uri url)
    {
        using var query = connection.CreateCommand(@"
            SELECT url, title
            FROM article_adds
            WHERE url = @url
        ");

        query.AddParameter("@url", url);

        PendingArticleAdd? result = null;
        using var row = query.ExecuteReader();
        if(row.Read())
        {
            result = PendingArticleAdd.FromRow(row);
        }

        return result;
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