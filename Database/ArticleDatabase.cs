using System.Data;

namespace Codevoid.Storyvoid;

internal sealed partial class ArticleDatabase : IArticleDatabase
{
    private static readonly DateTime UnixEpochStart = new DateTime(1970, 1, 1);

    private IDbConnection connection;
    private IInstapaperDatabase database;

    public event EventHandler<DatabaseArticle>? ArticleLikeStatusChanged;
    public event EventHandler<long>? ArticleDeleted;
    public event EventHandler<(DatabaseArticle Article, long DestinationLocalFolderId)>? ArticleMovedToFolder;

    internal ArticleDatabase(IDbConnection connection, IInstapaperDatabase database)
    {
        this.connection = connection;
        this.database = database;
    }

    /// <inheritdoc/>
    public IList<DatabaseArticle> ListArticlesForLocalFolder(long localFolderId)
    {
        var c = this.connection;

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

    /// <inheritdoc/>
    public IList<DatabaseArticle> ListLikedArticle()
    {
        var c = this.connection;

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
        var c = this.connection;
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

    /// <inheritdoc/>
    public DatabaseArticle AddArticleToFolder(
        ArticleRecordInformation data,
        long localFolderId
    )
    {
        var c = this.connection;

        if (this.database.FolderDatabase.GetFolderByLocalId(localFolderId) is null)
        {
            throw new FolderNotFoundException(localFolderId);
        }

        using var query = c.CreateCommand(@"
            INSERT INTO articles(id, title, url, description, read_progress, read_progress_timestamp, hash, liked)
            VALUES (@id, @title, @url, @description, @readProgress, @readProgressTimestamp, @hash, @liked);

            SELECT last_insert_rowid();
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

        using var pairWithFolderQuery = c.CreateCommand(@"
            INSERT INTO article_to_folder(local_folder_id, article_id)
            VALUES (@localFolderId, @articleId);
        ");

        pairWithFolderQuery.AddParameter("@articleId", data.id);
        pairWithFolderQuery.AddParameter("@localFolderId", localFolderId);

        pairWithFolderQuery.ExecuteNonQuery();

        return this.GetArticleById(data.id)!;
    }

    /// <inheritdoc />
    public DatabaseArticle UpdateArticle(ArticleRecordInformation updatedData)
    {
        var c = this.connection;

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

        return this.GetArticleById(updatedData.id)!;
    }

    private DatabaseArticle UpdateLikeStatusForArticle(long id, bool liked)
    {
        var c = this.connection;
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
            this.RaiseArticleLikeStatusChanged(updatedArticle!);
        }

        return updatedArticle!;
    }

    /// <inheritdoc/>
    public DatabaseArticle LikeArticle(long id)
    {
        return this.UpdateLikeStatusForArticle(id, true);
    }

    /// <inheritdoc/>
    public DatabaseArticle UnlikeArticle(long id)
    {
        return this.UpdateLikeStatusForArticle(id, false);
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

        var c = this.connection;

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


        return this.GetArticleById(articleId)!;
    }

    /// <inheritdoc/>
    public void MoveArticleToFolder(long articleId, long localFolderId)
    {
        var c = this.connection;

        if (this.database.FolderDatabase.GetFolderByLocalId(localFolderId) is null)
        {
            throw new FolderNotFoundException(localFolderId);
        }

        if (this.GetArticleById(articleId) is null)
        {
            throw new ArticleNotFoundException(articleId);
        }


        using var query = c.CreateCommand(@"
            UPDATE article_to_folder
            SET local_folder_id = @localFolderId
            WHERE article_id = @articleId AND local_folder_id <> @localFolderId;
        ");

        query.AddParameter("@articleId", articleId);
        query.AddParameter("@localFolderId", localFolderId);

        var impactedRows = query.ExecuteNonQuery();
        if(impactedRows > 0)
        {
            // Only raise (and get the article) if we actually affected a move
            var article = this.GetArticleById(articleId)!;
            this.RaiseArticleMovedToFolder(article, localFolderId);
        }
    }

    /// <inheritdoc/>
    public void DeleteArticle(long articleId)
    {
        var c = this.connection;

        void RemoveFromFolder()
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

        RemoveFromFolder();
        // Delete the local only state first, since that has a foreign
        // key relationship to the articles table. This is expected
        // to not throw an error if there is no local state associated
        // with the article being deleted.
        this.DeleteLocalOnlyArticleState(articleId);
        var wasDeleted = DeleteArticle();

        if (wasDeleted)
        {
            // Only raise the event if we actually deleted something
            this.RaiseArticleDeleted(articleId);
        }
    }

    #region Event Helpers
    private void RaiseArticleLikeStatusChanged(DatabaseArticle article)
    {
        var handlers = this.ArticleLikeStatusChanged;
        if(handlers is null)
        {
            return;
        }

        handlers(this, article);
    }

    private void RaiseArticleDeleted(long articleId)
    {
        var handlers = this.ArticleDeleted;
        if(handlers is null)
        {
            return;
        }

        handlers(this, articleId);
    }

    private void RaiseArticleMovedToFolder(DatabaseArticle article, long destinationLocalFolderId)
    {
        var handlers = this.ArticleMovedToFolder;
        if(handlers is null)
        {
            return;
        }

        handlers(this, (article, destinationLocalFolderId));
    }
    #endregion
}