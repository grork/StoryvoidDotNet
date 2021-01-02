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

        private static DatabaseLocalOnlyBookmarkState? GetLocalOnlyByBookmarkId(IDbConnection connection, long bookmarkId)
        {
            using var query = connection.CreateCommand(@"
                SELECT *
                FROM bookmark_local_only_state
                WHERE bookmark_id = @bookmarkId
            ");

            query.AddParameter("@bookmarkId", bookmarkId);

            using var row = query.ExecuteReader();
            DatabaseLocalOnlyBookmarkState? localOnlyState = null;
            if(row.Read())
            {
                localOnlyState = DatabaseLocalOnlyBookmarkState.FromRow(row);
            }

            return localOnlyState;
        }

        public Task<DatabaseLocalOnlyBookmarkState?> GetLocalOnlyStateByBookmarkIdAsync(long bookmarkId)
        {
            this.ThrowIfNotReady();

            var c = this.connection;
            return Task.Run(() => GetLocalOnlyByBookmarkId(c, bookmarkId));
        }

        public Task<DatabaseLocalOnlyBookmarkState> AddLocalOnlyStateForBookmarkAsync(DatabaseLocalOnlyBookmarkState localOnlyBookmarkState)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            void AddLocalyOnlyState()
            {
                using var query = c.CreateCommand(@"
                INSERT INTO bookmark_local_only_state(bookmark_id,
                                                      available_locally,
                                                      first_image_local_path,
                                                      first_image_remote_path,
                                                      local_path,
                                                      extracted_description,
                                                      article_unavailable,
                                                      include_in_mru)
                VALUES (@bookmarkId,
                        @availableLocally,
                        @firstImageLocalPath,
                        @firstImageRemotePath,
                        @localPath,
                        @extractedDescription,
                        @articleUnavailable,
                        @includeInMRU)
                ");

                query.AddParameter("@bookmarkId", localOnlyBookmarkState.BookmarkId);
                query.AddParameter("@availableLocally", localOnlyBookmarkState.AvailableLocally);
                query.AddParameter("@firstImageLocalPath", localOnlyBookmarkState.FirstImageLocalPath);
                query.AddParameter("@firstImageRemotePath", localOnlyBookmarkState.FirstImageRemoteUri);
                query.AddParameter("@localPath", localOnlyBookmarkState.LocalPath);
                query.AddParameter("@extractedDescription", localOnlyBookmarkState.ExtractedDescription);
                query.AddParameter("@articleUnavailable", localOnlyBookmarkState.ArticleUnavailable);
                query.AddParameter("@includeInMRU", localOnlyBookmarkState.IncludeInMRU);

                try
                {
                    query.ExecuteNonQuery();
                }
                // When the bookmark is missing, we get a foreign key constraint
                // error. We need to turn this into a strongly typed error.
                catch(SqliteException ex) when (ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_FOREIGNKEY)
                {
                    throw new BookmarkNotFoundException(localOnlyBookmarkState.BookmarkId);
                }
                // When local only state already exists, we need to convert the
                // primary key constraint error into something strongly typed
                catch(SqliteException ex) when (ex.SqliteErrorCode == SQLITE_CONSTRAINT && ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_PRIMARYKEY)
                {
                    throw new LocalOnlyStateExistsException(localOnlyBookmarkState.BookmarkId);
                }
            }

            return Task.Run(() => {
                AddLocalyOnlyState();

                return GetLocalOnlyByBookmarkId(c, localOnlyBookmarkState.BookmarkId)!;
            });
        }
    }
}