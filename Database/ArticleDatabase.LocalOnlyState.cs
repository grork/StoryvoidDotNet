using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

internal sealed partial class ArticleDatabase
{
    /// <inheritdoc/>
    public IEnumerable<DatabaseArticle> ListArticlesWithoutLocalOnlyState()
    {
        return ListArticlesWithoutLocalOnlyState(this.connection);
    }

    private IEnumerable<DatabaseArticle> ListArticlesWithoutLocalOnlyState(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM articles_with_local_only_state
            WHERE article_id is null
            ORDER BY id
        ");

        var results = new List<DatabaseArticle>();
        using var rows = query.ExecuteReader();
        while (rows.Read())
        {
            results.Add(DatabaseArticle.FromRow(rows));
        }

        return results;
    }

    /// <inheritdoc/>
    public DatabaseLocalOnlyArticleState? GetLocalOnlyStateByArticleId(long articleId)
    {
        return GetLocalOnlyStateByArticleId(this.connection, articleId);
    }

    private static DatabaseLocalOnlyArticleState? GetLocalOnlyStateByArticleId(IDbConnection c, long articleId)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM article_local_only_state
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);

        using var row = query.ExecuteReader();
        DatabaseLocalOnlyArticleState? localOnlyState = null;
        if (row.Read())
        {
            localOnlyState = DatabaseLocalOnlyArticleState.FromRow(row);
        }

        return localOnlyState;
    }

    /// <inheritdoc/>
    public DatabaseLocalOnlyArticleState AddLocalOnlyStateForArticle(DatabaseLocalOnlyArticleState localOnlyArticleState)
    {
        var updatedState = AddLocalOnlyStateForArticle(this.connection, localOnlyArticleState);

        if (this.eventSource is not null)
        {
            var article = this.GetArticleById(localOnlyArticleState.ArticleId)!;
            this.eventSource.RaiseArticleUpdated(article);
        }

        return updatedState;
    }

    private static DatabaseLocalOnlyArticleState AddLocalOnlyStateForArticle(IDbConnection c, DatabaseLocalOnlyArticleState localOnlyArticleState)
    {
        using var query = c.CreateCommand(@"
            INSERT INTO article_local_only_state(article_id,
                                                    available_locally,
                                                    first_image_local_path,
                                                    first_image_remote_path,
                                                    local_path,
                                                    extracted_description,
                                                    article_unavailable,
                                                    include_in_mru)
            VALUES (@articleId,
                    @availableLocally,
                    @firstImageLocalPath,
                    @firstImageRemotePath,
                    @localPath,
                    @extractedDescription,
                    @articleUnavailable,
                    @includeInMRU)
        ");

        using var t = query.BeginTransactionIfNeeded();

        query.AddParameter("@articleId", localOnlyArticleState.ArticleId);
        query.AddParameter("@availableLocally", localOnlyArticleState.AvailableLocally);
        query.AddParameter("@firstImageLocalPath", localOnlyArticleState.FirstImageLocalPath);
        query.AddParameter("@firstImageRemotePath", localOnlyArticleState.FirstImageRemoteUri);
        query.AddParameter("@localPath", localOnlyArticleState.LocalPath);
        query.AddParameter("@extractedDescription", localOnlyArticleState.ExtractedDescription);
        query.AddParameter("@articleUnavailable", localOnlyArticleState.ArticleUnavailable);
        query.AddParameter("@includeInMRU", localOnlyArticleState.IncludeInMRU);

        try
        {
            query.ExecuteNonQuery();
        }
        // When the article is missing, we get a foreign key constraint
        // error. We need to turn this into a strongly typed error.
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            throw new ArticleNotFoundException(localOnlyArticleState.ArticleId);
        }
        // When local only state already exists, we need to convert the
        // primary key constraint error into something strongly typed
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_PRIMARYKEY)
        {
            throw new LocalOnlyStateExistsException(localOnlyArticleState.ArticleId);
        }

        var result = GetLocalOnlyStateByArticleId(c, localOnlyArticleState.ArticleId)!;
        t?.Commit();

        return result;
    }

    /// <inheritdoc/>
    public void DeleteLocalOnlyArticleState(long articleId)
    {
        var wasDeleted = DeleteLocalOnlyArticleState(this.connection, articleId);
        if (wasDeleted && this.eventSource is not null)
        {
            var article = this.GetArticleById(articleId);
            this.eventSource.RaiseArticleUpdated(article!);
        }
    }

    private static bool DeleteLocalOnlyArticleState(IDbConnection c, long articleId)
    {
        using var query = c.CreateCommand(@"
            DELETE FROM article_local_only_state
            WHERE article_id = @articleId
        ");

        query.AddParameter("@articleId", articleId);
        return (query.ExecuteNonQuery() > 0);
    }

    public DatabaseLocalOnlyArticleState UpdateLocalOnlyArticleState(DatabaseLocalOnlyArticleState updatedLocalOnlyArticleState)
    {
        if (updatedLocalOnlyArticleState.ArticleId < 1)
        {
            throw new ArgumentException("Article ID must be greater than 0");
        }

        var updatedState = UpdateLocalOnlyArticleState(this.connection, updatedLocalOnlyArticleState);
        if (this.eventSource is not null)
        {
            var article = this.GetArticleById(updatedState.ArticleId);
            this.eventSource.RaiseArticleUpdated(article!);
        }

        return updatedState;
    }

    private static DatabaseLocalOnlyArticleState UpdateLocalOnlyArticleState(IDbConnection c, DatabaseLocalOnlyArticleState updatedLocalOnlyArticleState)
    {
        var articleId = updatedLocalOnlyArticleState.ArticleId;

        using var query = c.CreateCommand(@"
            UPDATE article_local_only_state SET
                available_locally = @availableLocally,
                first_image_local_path = @firstImageLocalPath,
                first_image_remote_path = @firstImageRemotePath,
                local_path = @localPath,
                extracted_description = @extractedDescription,
                article_unavailable = @articleUnavailable,
                include_in_mru = @includeInMru
            WHERE article_id = @articleId
        ");

        using var t = query.BeginTransactionIfNeeded();

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@availableLocally", updatedLocalOnlyArticleState.AvailableLocally);
        query.AddParameter("@firstImageLocalPath", updatedLocalOnlyArticleState.FirstImageLocalPath);
        query.AddParameter("@firstImageRemotePath", updatedLocalOnlyArticleState.FirstImageRemoteUri);
        query.AddParameter("@localPath", updatedLocalOnlyArticleState.LocalPath);
        query.AddParameter("@extractedDescription", updatedLocalOnlyArticleState.ExtractedDescription);
        query.AddParameter("@articleUnavailable", updatedLocalOnlyArticleState.ArticleUnavailable);
        query.AddParameter("@includeInMru", updatedLocalOnlyArticleState.IncludeInMRU);

        var updatedRows = query.ExecuteNonQuery();
        if (updatedRows < 1)
        {
            // Nothing was updated; check if it was just that there was
            // no existing state to update
            var state = GetLocalOnlyStateByArticleId(c, articleId);
            if (state is null)
            {
                throw new LocalOnlyStateNotFoundException(articleId);
            }

            throw new InvalidOperationException("Unknown error while updating local only state");
        }

        var local = GetLocalOnlyStateByArticleId(c, articleId);

        t?.Commit();

        return local!;
    }
}