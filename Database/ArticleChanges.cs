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
            throw new DuplicatePendingArticleAddException(url);
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

    /// <inheritdoc />
    public long CreatePendingArticleDelete(long articleId)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            INSERT INTO article_deletes(id)
            VALUES (@articleId)
        ");

        query.AddParameter("@articleId", articleId);

        try
        {
            query.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new DuplicatePendingArticleDeleteException(articleId);
        }

        return articleId;
    }

    /// <inheritdoc />
    public bool HasPendingArticleDelete(long articleId)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            SELECT id
            FROM article_deletes
            WHERE id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        using var row = query.ExecuteReader();
        return row.Read();
    }

    /// <inheritdoc />
    public IList<long> ListPendingArticleDeletes()
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_deletes
        ");

        using var row = query.ExecuteReader();
        var result = new List<long>();
        while(row.Read())
        {
            var articleId = row.GetInt64("id");
            result.Add(articleId);
        }

        return result;
    }

    /// <inheritdoc />
    public void RemovePendingArticleDelete(long articleId)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            DELETE FROM article_deletes
            WHERE id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        query.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public PendingArticleStateChange CreatePendingArticleStateChange(long articleId, bool liked)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            INSERT INTO article_liked_changes(article_id, liked)
            VALUES (@articleId, @liked)
        ");

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@liked", liked);

        try
        {
            query.ExecuteNonQuery();
            return GetPendingArticleStateChangeByArticleId(c, articleId)!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new DuplicatePendingArticleStateChangeException(articleId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            throw new ArticleNotFoundException(articleId);
        }
    }

    /// <inheritdoc />
    public PendingArticleStateChange? GetPendingArticleStateChangeByArticleId(long articleId)
    {
        var c = this.connection;
        return GetPendingArticleStateChangeByArticleId(c, articleId);
    }

    /// <inheritdoc />
    public IList<PendingArticleStateChange> ListPendingArticleStateChanges()
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_liked_changes
        ");

        using var row = query.ExecuteReader();
        var result = new List<PendingArticleStateChange>();
        while(row.Read())
        {
            var pendingArticleStateChange = PendingArticleStateChange.FromRow(row);
            result.Add(pendingArticleStateChange);
        }

        return result;
    }

    /// <inheritdoc />
    public void RemovePendingArticleStateChange(long articleId)
    {
        var c = this.connection;
        using var query = c.CreateCommand(@"
            DELETE FROM article_liked_changes
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        query.ExecuteNonQuery();
    }

    private static PendingArticleStateChange? GetPendingArticleStateChangeByArticleId(IDbConnection connection, long articleId)
    {
        using var query = connection.CreateCommand(@"
            SELECT article_id, liked
            FROM article_liked_changes
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        PendingArticleStateChange? result = null;
        using var row = query.ExecuteReader();
        if(row.Read())
        {
            result = PendingArticleStateChange.FromRow(row);
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