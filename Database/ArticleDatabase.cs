using System.Data;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid;

internal sealed partial class ArticleDatabase : IArticleDatabaseWithTransactionEvents
{
    private static readonly DateTime UnixEpochStart = new DateTime(1970, 1, 1);

    private IDbConnection connection;

    public event EventHandler<DatabaseArticle>? ArticleLikeStatusChangedWithinTransaction;
    public event EventHandler<long>? ArticleDeletedWithinTransaction;
    public event EventHandler<(DatabaseArticle Article, long DestinationLocalFolderId)>? ArticleMovedToFolderWithinTransaction;

    internal ArticleDatabase(IDbConnection connection)
    {
        this.connection = connection;
    }

    /// <inheritdoc/>
    public IList<DatabaseArticle> ListArticlesForLocalFolder(long localFolderId)
    {
        return ListArticlesForLocalFolder(this.connection, localFolderId);
    }

    private static IList<DatabaseArticle> ListArticlesForLocalFolder(IDbConnection c, long localFolderId)
    {
        using var query = c.CreateCommand(@"
            SELECT a.*
            FROM article_to_folder
            INNER JOIN articles_with_local_only_state a
                ON article_to_folder.article_id = a.id
            WHERE article_to_folder.local_folder_id = @localFolderId
        ");

        query.AddParameter("@localFolderId", localFolderId);

        var results = new List<DatabaseArticle>();
        using var rows = query.ExecuteReader();
        while (rows.Read())
        {
            results.Add(DatabaseArticle.FromRow(rows));
        }

        return results;
    }

    /// <inheritdoc />
    public IList<(DatabaseArticle Article, long LocalFolderId)> ListAllArticlesInAFolder()
    {
        return ListAllArticlesInAFolder(this.connection);
    }

    private static IList<(DatabaseArticle Article, long LocalFolderId)> ListAllArticlesInAFolder(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT a.*, article_to_folder.local_folder_id
            FROM article_to_folder
            INNER JOIN articles_with_local_only_state a
                ON article_to_folder.article_id = a.id
        ");

        var results = new List<(DatabaseArticle, long)>();
        using var rows = query.ExecuteReader();
        while (rows.Read())
        {
            var article = DatabaseArticle.FromRow(rows);
            var folderId = rows.GetInt64("local_folder_id");
            results.Add((article, folderId));
        }

        return results;
    }

    /// <inheritdoc />
    public IList<DatabaseArticle> ListArticlesNotInAFolder()
    {
        return ListArticlesNotInAFolder(this.connection);
    }

    private static IList<DatabaseArticle> ListArticlesNotInAFolder(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM articles
            WHERE id NOT IN (SELECT article_id FROM article_to_folder)
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
    public IList<DatabaseArticle> ListLikedArticles()
    {
        return ListLikedArticles(this.connection);
    }

    private static IList<DatabaseArticle> ListLikedArticles(IDbConnection c)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM articles_with_local_only_state
            WHERE liked = true
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
    public DatabaseArticle? GetArticleById(long id)
    {
        return GetArticleById(this.connection, id);
    }

    private static DatabaseArticle? GetArticleById(IDbConnection c, long id)
    {
        using var query = c.CreateCommand(@"
            SELECT *
            FROM articles_with_local_only_state
            WHERE id = @id
        ");

        query.AddParameter("@id", id);

        using var row = query.ExecuteReader();

        DatabaseArticle? article = null;
        if (row.Read())
        {
            article = DatabaseArticle.FromRow(row);
        }

        return article;
    }

    /// <inheritdoc />
    public DatabaseArticle AddArticleNoFolder(ArticleRecordInformation data)
    {
        return AddArticleNoFolder(this.connection, data);
    }

    private static DatabaseArticle AddArticleNoFolder(IDbConnection c, ArticleRecordInformation data)
    {
        using var t = c.BeginTransaction();

        AddArticle(c, data);

        var addedArticle = GetArticleById(c, data.id)!;

        t.Commit();

        return addedArticle;
    }

    private static void AddArticle(IDbConnection connection, ArticleRecordInformation data)
    {
        using var query = connection.CreateCommand(@"
            INSERT INTO articles(id, title, url, description, read_progress, read_progress_timestamp, hash, liked)
            VALUES (@id, @title, @url, @description, @readProgress, @readProgressTimestamp, @hash, @liked);
        ");

        query.AddParameter("@id", data.id);
        query.AddParameter("@title", data.title);
        query.AddParameter("@url", data.url);
        query.AddParameter("@description", data.description);
        query.AddParameter("@readProgress", data.readProgress);
        query.AddParameter("@readProgressTimestamp", data.readProgressTimestamp);
        query.AddParameter("@hash", data.hash);
        query.AddParameter("@liked", data.liked);

        query.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public DatabaseArticle AddArticleToFolder(
        ArticleRecordInformation data,
        long localFolderId
    )
    {
        return AddArticleToFolder(this.connection, data, localFolderId);
    }

    private static DatabaseArticle AddArticleToFolder(IDbConnection c, ArticleRecordInformation data, long localFolderId)
    {
        using var t = c.BeginTransaction();

        AddArticle(c, data);

        using var pairWithFolderQuery = c.CreateCommand(@"
            INSERT INTO article_to_folder(local_folder_id, article_id)
            VALUES (@localFolderId, @articleId);
        ");

        pairWithFolderQuery.AddParameter("@articleId", data.id);
        pairWithFolderQuery.AddParameter("@localFolderId", localFolderId);

        try
        {
            pairWithFolderQuery.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            // Assume that this isn't about the article failing the foreign key
            // constraint
            throw new FolderNotFoundException(localFolderId);
        }

        var addedArticle = GetArticleById(c, data.id)!;
        t.Commit();

        return addedArticle;
    }

    /// <inheritdoc />
    public DatabaseArticle UpdateArticle(ArticleRecordInformation updatedData)
    {
        return UpdateArticle(this.connection, updatedData);
    }

    private static DatabaseArticle UpdateArticle(IDbConnection c, ArticleRecordInformation updatedData)
    {
        using var t = c.BeginTransaction();

        using var query = c.CreateCommand(@"
            UPDATE articles SET
                url = @url,
                title = @title,
                description = @description,
                read_progress = @readProgress,
                read_progresS_timestamp = @readProgressTimestamp,
                hash = @hash,
                liked = @liked
            WHERE id = @id
        ");

        query.AddParameter("@id", updatedData.id);
        query.AddParameter("@url", updatedData.url);
        query.AddParameter("@title", updatedData.title);
        query.AddParameter("@description", updatedData.description);
        query.AddParameter("@readProgress", updatedData.readProgress);
        query.AddParameter("@readProgressTimestamp", updatedData.readProgressTimestamp);
        query.AddParameter("@hash", updatedData.hash);
        query.AddParameter("@liked", updatedData.liked);


        var impactedRows = query.ExecuteNonQuery();
        if (impactedRows < 1)
        {
            throw new ArticleNotFoundException(updatedData.id);
        }

        var updatedArticle = GetArticleById(c, updatedData.id)!;

        t.Commit();

        return updatedArticle;
    }

    /// <inheritdoc/>
    public DatabaseArticle LikeArticle(long id)
    {
        return UpdateLikeStatusForArticle(this.connection, id, true);
    }

    /// <inheritdoc/>
    public DatabaseArticle UnlikeArticle(long id)
    {
        return UpdateLikeStatusForArticle(this.connection, id, false);
    }

    private DatabaseArticle UpdateLikeStatusForArticle(IDbConnection c, long id, bool liked)
    {
        using var t = c.BeginTransaction();

        using var query = c.CreateCommand(@"
            UPDATE articles
            SET liked = @liked
            WHERE id = @id AND liked <> @liked
        ");

        query.AddParameter("@id", id);
        query.AddParameter("@liked", liked);

        var impactedRows = query.ExecuteNonQuery();

        var updatedArticle = this.GetArticleById(id);
        if (updatedArticle is null)
        {
            // We don't need to check anything if we can't get the article back
            // again
            throw new ArticleNotFoundException(id);
        }

        if (impactedRows > 0)
        {
            // Only raise the change event if the table was actually updated
            this.RaiseArticleLikeStatusChangedWithinTransaction(updatedArticle!);
        }

        t.Commit();

        return updatedArticle!;
    }

    /// <inheritdoc/>
    public DatabaseArticle UpdateReadProgressForArticle(float readProgress, DateTime readProgressTimestamp, long articleId)
    {
        if (readProgress < 0.0 || readProgress > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(readProgress), "Progress must be between 0.0 and 1.0");
        }

        if (readProgressTimestamp < UnixEpochStart)
        {
            throw new ArgumentOutOfRangeException(nameof(readProgressTimestamp), "Progress Timestamp must be within the Unix Epoch");
        }

        return UpdateReadProgressForArticle(this.connection, readProgress, readProgressTimestamp, articleId);
    }

    private static DatabaseArticle UpdateReadProgressForArticle(IDbConnection c, float readProgress, DateTime readProgressTimestamp, long articleId)
    {
        using var t = c.BeginTransaction();

        // The hash field is driven by the service, and complately opaque
        // to us. The hash is also how the service determines if progress
        // needs to be updated in list calls. This means that whenever
        // we update the progress of a article, if we don't have supplied
        // hash, we need to stomp that hash since it's different, but we
        // have no idea how to recompute it.
        // To simulate a new hash, we will generate a random number, and
        // use that as the hash.
        var r = new Random();
        var fauxHash = r.Next().ToString();

        using var query = c.CreateCommand(@"
            UPDATE articles
            SET read_progress = @readProgress, read_progress_timestamp = @readProgressTimestamp, hash = @hash
            WHERE id = @id
        ");

        query.AddParameter("@id", articleId);
        query.AddParameter("@readProgress", readProgress);
        query.AddParameter("@readProgressTimestamp", readProgressTimestamp);
        query.AddParameter("@hash", fauxHash);

        var impactedRows = query.ExecuteNonQuery();
        if (impactedRows < 1)
        {
            throw new ArticleNotFoundException(articleId);
        }

        var updatedArticle = GetArticleById(c, articleId)!;

        t.Commit();

        return updatedArticle;
    }

    /// <inheritdoc/>
    public void MoveArticleToFolder(long articleId, long localFolderId)
    {
        MoveArticleToFolder(this.connection, articleId, localFolderId, this);
    }

    private static void MoveArticleToFolder(IDbConnection c, long articleId, long localFolderId, ArticleDatabase eventSource)
    {
        using var t = c.BeginTransaction();

        if (GetArticleById(c, articleId) is null)
        {
            throw new ArticleNotFoundException(articleId);
        }

        // Check and see if the article is already in the destination folder. If
        // it is, it means we have nothing to do, and we can just go home.
        using (var isAlreadyInTargetFolder = c.CreateCommand(@"
            SELECT count(*) FROM article_to_folder
            WHERE article_id = @articleId AND local_folder_id = @localFolderId
        "))
        {
            isAlreadyInTargetFolder.AddParameter("@articleId", articleId);
            isAlreadyInTargetFolder.AddParameter("@localFolderId", localFolderId);

            var folderPairs = (long)(isAlreadyInTargetFolder.ExecuteScalar()!);
            if (folderPairs == 1)
            {
                return;
            }
        }

        // If there *is* a folder reference, we need to delete it first, so we
        // we can have a single path for placing in a folder (e.g. no UPDATE vs
        // INSERT dance).

        using (var removeFromExistingFolder = c.CreateCommand(@"
            DELETE FROM article_to_folder
            WHERE article_id = @articleId
        "))
        {
            removeFromExistingFolder.AddParameter("@articleId", articleId);
            removeFromExistingFolder.ExecuteNonQuery();
        }

        using var query = c.CreateCommand(@"
            INSERT INTO article_to_folder(article_id, local_folder_id)
            VALUES (@articleId, @localFolderId);
        ");

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@localFolderId", localFolderId);

        try
        {
            query.ExecuteNonQuery();

            var article = GetArticleById(c, articleId)!;
            eventSource.RaiseArticleMovedToFolderWithinTransaction(article, localFolderId);

            t.Commit();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT
                                     && ex.SqliteExtendedErrorCode == SqliteErrorCodes.SQLITE_CONSTRAINT_FOREIGNKEY)
        {
            // Assume that this isn't about the article failing the foreign key
            // constraint
            throw new FolderNotFoundException(localFolderId);
        }
    }

    /// <inheritdoc/>
    public void DeleteArticle(long articleId)
    {
        DeleteArticle(this.connection, articleId, this);
    }

    private static void DeleteArticle(IDbConnection c, long articleId, ArticleDatabase eventSource)
    {
        using var t = c.BeginTransaction();

        void DeleteFromFolder()
        {
            using var query = c.CreateCommand(@"
                DELETE FROM article_to_folder
                WHERE article_id = @articleId
            ");

            query.AddParameter("@articleId", articleId);

            query.ExecuteNonQuery();
        }

        bool DeleteArticle()
        {
            // Now that we've deleted the local state, we can delete the
            // article itself.
            using var query = c.CreateCommand(@"
                DELETE FROM articles
                WHERE id = @id
            ");

            query.AddParameter("@id", articleId);

            return (query.ExecuteNonQuery() > 0);
        }

        DeleteFromFolder();

        // Delete the local only state first, since that has a foreign
        // key relationship to the articles table. This is expected
        // to not throw an error if there is no local state associated
        // with the article being deleted.
        DeleteLocalOnlyArticleState(c, articleId);
        var wasDeleted = DeleteArticle();

        if (wasDeleted)
        {
            // Only raise the event if we actually deleted something
            eventSource.RaiseArticleDeletedWithinTransaction(articleId);
        }

        t.Commit();
    }

    #region Event Helpers
    private void RaiseArticleLikeStatusChangedWithinTransaction(DatabaseArticle article)
    {
        var handlers = this.ArticleLikeStatusChangedWithinTransaction;
        handlers?.Invoke(this, article);
    }

    private void RaiseArticleDeletedWithinTransaction(long articleId)
    {
        var handlers = this.ArticleDeletedWithinTransaction;
        handlers?.Invoke(this, articleId);
    }

    private void RaiseArticleMovedToFolderWithinTransaction(DatabaseArticle article, long destinationLocalFolderId)
    {
        var handlers = this.ArticleMovedToFolderWithinTransaction;
        handlers?.Invoke(this, (article, destinationLocalFolderId));
    }
    #endregion
}