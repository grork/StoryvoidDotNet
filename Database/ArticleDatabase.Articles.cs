using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    sealed partial class ArticleDatabase
    {
        private static readonly DateTime UnixEpochStart = new DateTime(1970, 1, 1);

        /// <inheritdoc/>
        public Task<IList<DatabaseArticle>> ListArticlesForLocalFolderAsync(long localFolderId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            IList<DatabaseArticle> GetArticles()
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

            return Task.Run(GetArticles);
        }

        /// <inheritdoc/>
        public Task<IList<DatabaseArticle>> ListLikedArticleAsync()
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            IList<DatabaseArticle> GetArticles()
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

            return Task.Run(GetArticles);
        }

        /// <inheritdoc/>
        public Task<DatabaseArticle?> GetArticleByIdAsync(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            return Task.Run(() => ArticleDatabase.GetArticleById(c, id));
        }

        private static DatabaseArticle? GetArticleById(IDbConnection connection, long id)
        {
            using var query = connection.CreateCommand(@"
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
        public Task<DatabaseArticle> AddArticleToFolderAsync(
            ArticleRecordInformation data,
            long localFolderId
        )
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            void AddArticle()
            {
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
            }

            void PairArticleToFolder()
            {
                using var query = c.CreateCommand(@"
                    INSERT INTO article_to_folder(local_folder_id, article_id)
                    VALUES (@localFolderId, @articleId);
                ");

                query.AddParameter("@articleId", data.id);
                query.AddParameter("@localFolderId", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                if (GetFolderByLocalId(c, localFolderId) == null)
                {
                    throw new FolderNotFoundException(localFolderId);
                }

                AddArticle();
                PairArticleToFolder();
                return ArticleDatabase.GetArticleById(c, data.id)!;
            });
        }

        /// <inheritdoc />
        public Task<DatabaseArticle> UpdateArticleAsync(ArticleRecordInformation updatedData)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            void UpdateArticle()
            {
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
            }

            return Task.Run(() =>
            {
                UpdateArticle();
                return ArticleDatabase.GetArticleById(c, updatedData.id)!;
            });
        }

        private static void UpdateLikeStatusForArticle(IDbConnection c, long id, bool liked)
        {
            using var query = c.CreateCommand(@"
                UPDATE articles
                SET liked = @liked
                WHERE id = @id
            ");

            query.AddParameter("@id", id);
            query.AddParameter("@liked", liked);

            var impactedRows = query.ExecuteNonQuery();
            if (impactedRows < 1)
            {
                throw new ArticleNotFoundException(id);
            }
        }

        /// <inheritdoc/>
        public Task<DatabaseArticle> LikeArticleAsync(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForArticle(c, id, true);
                return ArticleDatabase.GetArticleById(c, id)!;
            });
        }

        /// <inheritdoc/>
        public Task<DatabaseArticle> UnlikeArticleAsync(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForArticle(c, id, false);
                return ArticleDatabase.GetArticleById(c, id)!;
            });
        }

        /// <inheritdoc/>
        public Task<DatabaseArticle> UpdateReadProgressForArticleAsync(float readProgress, DateTime readProgressTimestamp, long articleId)
        {
            if (readProgress < 0.0 || readProgress > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(readProgress), "Progress must be between 0.0 and 1.0");
            }

            if (readProgressTimestamp < UnixEpochStart)
            {
                throw new ArgumentOutOfRangeException(nameof(readProgressTimestamp), "Progress Timestamp must be within the Unix Epoch");
            }

            this.ThrowIfNotReady();

            var c = this.connection;

            void UpdateProgressForArticle()
            {
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
            }

            return Task.Run(() =>
            {
                UpdateProgressForArticle();
                return ArticleDatabase.GetArticleById(c, articleId)!;
            });
        }

        /// <inheritdoc/>
        public Task MoveArticleToFolderAsync(long articleId, long localFolderId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            void MoveArticleToFolder()
            {
                using var query = c.CreateCommand(@"
                    UPDATE article_to_folder
                    SET local_folder_id = @localFolderId
                    WHERE article_id = @articleId;
                ");

                query.AddParameter("@articleId", articleId);
                query.AddParameter("@localFolderId", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                if (GetFolderByLocalId(c, localFolderId) == null)
                {
                    throw new FolderNotFoundException(localFolderId);
                }

                if (ArticleDatabase.GetArticleById(c, articleId) == null)
                {
                    throw new ArticleNotFoundException(articleId);
                }

                MoveArticleToFolder();
            });
        }

        /// <inheritdoc/>
        public Task DeleteArticleAsync(long articleId)
        {
            this.ThrowIfNotReady();

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

            void DeleteArticle()
            {
                using var query = c.CreateCommand(@"
                    DELETE FROM articles
                    WHERE id = @id
                ");

                query.AddParameter("@id", articleId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                RemoveFromFolder();
                DeleteArticle();
            });
        }
    }
}