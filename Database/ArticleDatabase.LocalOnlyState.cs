using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid
{
    sealed partial class ArticleDatabase
    {
        private static int SQLITE_CONSTRAINT = 19;
        private static int SQLITE_CONSTRAINT_FOREIGNKEY = 787;
        private static int SQLITE_CONSTRAINT_PRIMARYKEY = 1555;

        private static DatabaseLocalOnlyArticleState? GetLocalOnlyByArticleId(IDbConnection connection, long articleId)
        {
            using var query = connection.CreateCommand(@"
                SELECT *
                FROM article_local_only_state
                WHERE article_id = @articleId
            ");

            query.AddParameter("@articleId", articleId);

            using var row = query.ExecuteReader();
            DatabaseLocalOnlyArticleState? localOnlyState = null;
            if(row.Read())
            {
                localOnlyState = DatabaseLocalOnlyArticleState.FromRow(row);
            }

            return localOnlyState;
        }

        public Task<DatabaseLocalOnlyArticleState?> GetLocalOnlyStateByArticleIdAsync(long articleId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            return Task.Run(() => GetLocalOnlyByArticleId(c, articleId));
        }

        public Task<DatabaseLocalOnlyArticleState> AddLocalOnlyStateForArticleAsync(DatabaseLocalOnlyArticleState localOnlyArticleState)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            void AddLocalyOnlyState()
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
                catch(SqliteException ex) when (ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_FOREIGNKEY)
                {
                    throw new ArticleNotFoundException(localOnlyArticleState.ArticleId);
                }
                // When local only state already exists, we need to convert the
                // primary key constraint error into something strongly typed
                catch(SqliteException ex) when (ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_PRIMARYKEY)
                {
                    throw new LocalOnlyStateExistsException(localOnlyArticleState.ArticleId);
                }
            }

            return Task.Run(() => {
                AddLocalyOnlyState();

                return GetLocalOnlyByArticleId(c, localOnlyArticleState.ArticleId)!;
            });
        }

        public Task DeleteLocalOnlyArticleStateAsync(long articleId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            void DeleteLocalyOnlyState()
            {
                using var query = c.CreateCommand(@"
                    DELETE FROM article_local_only_state
                    WHERE article_id = @articleId
                ");

                query.AddParameter("@articleId", articleId);

                query.ExecuteNonQuery();
            }

            return Task.Run(DeleteLocalyOnlyState);
        }
    }
}