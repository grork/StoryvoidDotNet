using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

/// <inheritdoc />
internal class ArticleChanges : IArticleChangesDatabase
{
    private IDbConnection connection;

    internal ArticleChanges(IDbConnection connection)
    {
        this.connection = connection;
    }

    /// <inheritdoc />
    public PendingArticleAdd CreatePendingArticleAdd(Uri url, string? title)
    {
        return CreatePendingArticleAdd(this.connection, url, title);
    }

    private static PendingArticleAdd CreatePendingArticleAdd(IDbConnection c, Uri url, string? title)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO article_adds(url, title)
            VALUES (@url, @title);
        ");

        using var t = query.BeginTransactionIfNeeded();

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

            var result = GetPendingArticleAddByUrl(c, url)!;
            t?.Commit();

            return result;
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
        return GetPendingArticleAddByUrl(this.connection, url);
    }

    /// <inheritdoc />
    public IList<PendingArticleAdd> ListPendingArticleAdds()
    {
        return ListPendingArticleAdds(this.connection);
    }

    private static IList<PendingArticleAdd> ListPendingArticleAdds(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT * FROM article_adds;
        ");

        using var pendingArticleAdds = query.ExecuteReader();

        var result = new List<PendingArticleAdd>();
        while (pendingArticleAdds.Read())
        {
            var pendingArticleAdd = PendingArticleAdd.FromRow(pendingArticleAdds);
            result.Add(pendingArticleAdd);
        }

        return result;
    }

    /// <inheritdoc />
    public void DeletePendingArticleAdd(Uri url)
    {
        DeletePendingArticleAdd(this.connection, url);
    }

    private static void DeletePendingArticleAdd(IDbConnection c, Uri url)
    {
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
        if (row.Read())
        {
            result = PendingArticleAdd.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc />
    public long CreatePendingArticleDelete(long articleId)
    {
        return CreatePendingArticleDelete(this.connection, articleId);
    }

    private static long CreatePendingArticleDelete(IDbConnection c, long articleId)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO article_deletes(id)
            VALUES (@articleId)
        ");

        using var t = query.BeginTransactionIfNeeded();

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

        t?.Commit();
        return articleId;
    }

    /// <inheritdoc />
    public bool HasPendingArticleDelete(long articleId)
    {
        return HasPendingArticleDelete(this.connection, articleId);
    }

    private static bool HasPendingArticleDelete(IDbConnection c, long articleId)
    {
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
        return ListPendingArticleDeletes(this.connection);
    }

    private static IList<long> ListPendingArticleDeletes(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_deletes
        ");

        using var row = query.ExecuteReader();
        var result = new List<long>();
        while (row.Read())
        {
            var articleId = row.GetInt64("id");
            result.Add(articleId);
        }

        return result;
    }

    /// <inheritdoc />
    public void DeletePendingArticleDelete(long articleId)
    {
        DeletePendingArticleDelete(this.connection, articleId);
    }

    private static void DeletePendingArticleDelete(IDbConnection c, long articleId)
    {
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
        return CreatePendingArticleStateChange(this.connection, articleId, liked);
    }

    private static PendingArticleStateChange CreatePendingArticleStateChange(IDbConnection c, long articleId, bool liked)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO article_liked_changes(article_id, liked)
            VALUES (@articleId, @liked)
        ");

        using var t = query.BeginTransactionIfNeeded();

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@liked", liked);

        try
        {
            query.ExecuteNonQuery();
            var result = GetPendingArticleStateChangeByArticleId(c, articleId)!;

            t?.Commit();
            return result;
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
        return GetPendingArticleStateChangeByArticleId(this.connection, articleId);
    }

    /// <inheritdoc />
    public IList<PendingArticleStateChange> ListPendingArticleStateChanges()
    {
        return ListPendingArticleStateChanges(this.connection);
    }

    private static IList<PendingArticleStateChange> ListPendingArticleStateChanges(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_liked_changes
        ");

        using var row = query.ExecuteReader();
        var result = new List<PendingArticleStateChange>();
        while (row.Read())
        {
            var pendingArticleStateChange = PendingArticleStateChange.FromRow(row);
            result.Add(pendingArticleStateChange);
        }

        return result;
    }

    /// <inheritdoc />
    public void DeletePendingArticleStateChange(long articleId)
    {
        DeletePendingArticleStateChange(this.connection, articleId);
    }

    private static void DeletePendingArticleStateChange(IDbConnection c, long articleId)
    {
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
        if (row.Read())
        {
            result = PendingArticleStateChange.FromRow(row);
        }

        return result;
    }

    /// <inheritdoc />
    public PendingArticleMove CreatePendingArticleMove(long articleId, long destinationFolderLocalId)
    {
        return CreatePendingArticleMove(this.connection, articleId, destinationFolderLocalId);
    }

    private static PendingArticleMove CreatePendingArticleMove(IDbConnection c, long articleId, long destinationFolderLocalId)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO article_folder_changes(article_id, destination_local_id)
            VALUES (@articleId, @destinationFolderLocalId)
        ");

        using var t = query.BeginTransactionIfNeeded();

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@destinationFolderLocalId", destinationFolderLocalId);

        try
        {
            query.ExecuteNonQuery();
            var result = GetPendingArticleMoveByArticleId(c, articleId)!;
            t?.Commit();
            return result;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new DuplicatePendingArticleMoveException(articleId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            // We need to determine if it's a missing article or a missing
            // folder. The assumption is that if there *is* an article, then it
            // must be the folder thats awol.
            //
            // Why aren't we just using the article database wrapper directly?
            // To break the dependency on that class directly. Sure, taking a
            // dependency on the DB query itself is... well, a dependency... but
            // since we're already dependent on the configuration of the DB this
            // seems a fair trade off to remove the _code_ dependency.
            var articleExistsCommand = c.CreateCommand(@"
                SELECT COUNT(id) FROM articles
                WHERE id = @articleId
            ");

            articleExistsCommand.AddParameter("@articleId", articleId);

            var article = (long)articleExistsCommand.ExecuteScalar();
            if (article == 0)
            {
                throw new ArticleNotFoundException(articleId);
            }
            else
            {
                throw new FolderNotFoundException(destinationFolderLocalId);
            }
        }
    }

    /// <inheritdoc />
    public PendingArticleMove? GetPendingArticleMove(long articleId)
    {
        var c = this.connection;
        return GetPendingArticleMoveByArticleId(c, articleId);
    }

    /// <inheritdoc />
    public IList<PendingArticleMove> ListPendingArticleMoves()
    {
        return ListPendingArticleMoves(this.connection);
    }

    private static IList<PendingArticleMove> ListPendingArticleMoves(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_folder_changes
        ");

        using var row = query.ExecuteReader();
        var result = new List<PendingArticleMove>();
        while (row.Read())
        {
            var pendingArticleMove = PendingArticleMove.FromRow(row);
            result.Add(pendingArticleMove);
        }

        return result;
    }

    /// <inheritdoc />
    public IList<PendingArticleMove> ListPendingArticleMovesForLocalFolderId(long localFolderId)
    {
        return ListPendingArticleMovesForLocalFolderId(this.connection, localFolderId);
    }

    private static IList<PendingArticleMove> ListPendingArticleMovesForLocalFolderId(IDbConnection c, long localFolderId)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_folder_changes
            WHERE destination_local_id = @localFolderId
        ");

        query.AddParameter("@localFolderId", localFolderId);

        using var row = query.ExecuteReader();
        var result = new List<PendingArticleMove>();
        while (row.Read())
        {
            var pendingArticleMove = PendingArticleMove.FromRow(row);
            result.Add(pendingArticleMove);
        }

        return result;
    }

    /// <inheritdoc />
    public void DeletePendingArticleMove(long articleId)
    {
        DeletePendingArticleMove(this.connection, articleId);
    }

    private static void DeletePendingArticleMove(IDbConnection c, long articleId)
    {
        using var query = c.CreateCommand(@"
            DELETE FROM article_folder_changes
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        query.ExecuteNonQuery();
    }

    private static PendingArticleMove? GetPendingArticleMoveByArticleId(IDbConnection connection, long articleId)
    {
        using var query = connection.CreateCommand(@"
            SELECT article_id, destination_local_id
            FROM article_folder_changes
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        PendingArticleMove? result = null;
        using var row = query.ExecuteReader();
        if (row.Read())
        {
            result = PendingArticleMove.FromRow(row);
        }

        return result;
    }
}