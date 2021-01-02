using System.Data;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    sealed partial class ArticleDatabase
    {
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

                query.ExecuteNonQuery();
            }

            return Task.Run(() => {
                AddLocalyOnlyState();

                return GetLocalOnlyByBookmarkId(c, localOnlyBookmarkState.BookmarkId)!;
            });
        }
    }
}