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
        public Task<IList<DatabaseBookmark>> GetBookmarksForLocalFolderAsync(long localFolderId)
        {
            var c = this.connection;

            IList<DatabaseBookmark> GetBookmarks()
            {
                using var query = c.CreateCommand(@"
                    SELECT b.*
                    FROM bookmark_to_folder
                    INNER JOIN bookmarks b
                        ON bookmark_to_folder.bookmark_id = b.id
                    WHERE bookmark_to_folder.local_folder_id = @local_folder_id
                ");

                query.AddParameter("@local_folder_id", localFolderId);

                var results = new List<DatabaseBookmark>();
                using var rows = query.ExecuteReader();
                while (rows.Read())
                {
                    results.Add(DatabaseBookmark.FromRow(rows));
                }

                return results;
            }

            return Task.Run(GetBookmarks);
        }

        /// <inheritdoc/>
        public Task<IList<DatabaseBookmark>> GetLikedBookmarksAsync()
        {
            var c = this.connection;
            IList<DatabaseBookmark> GetBookmarks()
            {
                using var query = c!.CreateCommand(@"
                    SELECT *
                    FROM bookmarks
                    WHERE liked = true
                ");

                var results = new List<DatabaseBookmark>();
                using var rows = query.ExecuteReader();
                while(rows.Read())
                {
                    results.Add(DatabaseBookmark.FromRow(rows));
                }

                return results;
            }

            return Task.Run(GetBookmarks);
        }

        public Task<DatabaseBookmark?> GetBookmarkByIdAsync(long id)
        {
            var c = this.connection;
            return Task.Run(() => GetBookmarkByIdAsync(c, id));
        }

        private DatabaseBookmark? GetBookmarkByIdAsync(IDbConnection connection, long id)
        {
            using var query = connection.CreateCommand(@"
                SELECT *
                FROM bookmarks
                WHERE id = @id
            ");

            query.AddParameter("@id", id);

            using var row = query.ExecuteReader();

            DatabaseBookmark? bookmark = null;
            if (row.Read())
            {
                bookmark = DatabaseBookmark.FromRow(row);
            }

            return bookmark;
        }

        /// <inheritdoc/>
        public Task<DatabaseBookmark> AddBookmarkAsync(
            (int id, string title, Uri url, string description, float readProgress, DateTime readProgressTimestamp, string hash, bool liked) data,
            long localFolderId
        )
        {
            var c = this.connection;

            void AddBookmark()
            {
                using var query = c!.CreateCommand(@"
                    INSERT INTO bookmarks(id, title, url, description, read_progress, read_progress_timestamp, hash, liked)
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

            void PairBookmarkToFolder()
            {
                using var query = c!.CreateCommand(@"
                    INSERT INTO bookmark_to_folder(local_folder_id, bookmark_id)
                    VALUES (@local_folder_id, @bookmark_id);
                ");

                query.AddParameter("@bookmark_id", data.id);
                query.AddParameter("@local_folder_id", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                if(GetFolderByLocalId(c, localFolderId) == null)
                {
                    throw new FolderNotFoundException(localFolderId);
                }

                AddBookmark();
                PairBookmarkToFolder();
                return GetBookmarkByIdAsync(c, data.id)!;
            });
        }

        private static void UpdateLikeStatusForBookmark(IDbConnection c, long id, bool liked)
        {
            using var query = c!.CreateCommand(@"
                UPDATE bookmarks
                SET liked = @liked
                WHERE id = @id
            ");

            query.AddParameter("@id", id);
            query.AddParameter("@liked", liked);

            var impactedRows = query.ExecuteNonQuery();
            if(impactedRows < 1)
            {
                throw new BookmarkNotFoundException(id);
            }
        }

        /// <inheritdoc/>
        public Task<DatabaseBookmark> LikeBookmarkAsync(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForBookmark(c, id, true);
                return GetBookmarkByIdAsync(c, id)!;
            });
        }

        /// <inheritdoc/>
        public Task<DatabaseBookmark> UnlikeBookmarkAsync(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForBookmark(c, id, false);
                return GetBookmarkByIdAsync(c, id)!;
            });
        }

        public Task<DatabaseBookmark> UpdateReadProgressForBookmarkAsync(float readProgress, DateTime readProgressTimestamp, long bookmarkId)
        {
            if(readProgress < 0.0 || readProgress > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(readProgress), "Progress must be between 0.0 and 1.0");
            }

            if(readProgressTimestamp < UnixEpochStart)
            {
                throw new ArgumentOutOfRangeException(nameof(readProgressTimestamp), "Progress Timestamp must be within the Unix Epochs");
            }

            this.ThrowIfNotReady();

            var c = this.connection;
            void UpdateProgressForBookmark()
            {
                using var query = c!.CreateCommand(@"
                    UPDATE bookmarks
                    SET read_progress = @readProgress, read_progress_timestamp = @readProgressTimestamp
                    WHERE id = @id
                ");

                query.AddParameter("@id", bookmarkId);
                query.AddParameter("@readProgress", readProgress);
                query.AddParameter("@readProgressTimestamp", readProgressTimestamp);

                var impactedRows = query.ExecuteNonQuery();
                if(impactedRows < 1)
                {
                    throw new BookmarkNotFoundException(bookmarkId);
                }
            }

            return Task.Run(() =>
            {
                UpdateProgressForBookmark();
                return GetBookmarkByIdAsync(c, bookmarkId)!;
            });
        }

        public Task MoveBookmarkToFolderAsync(long bookmarkId, long localFolderId)
        {
            var c = this.connection;
            void MoveBookmarkToFolder()
            {
                using var query = c!.CreateCommand(@"
                    UPDATE bookmark_to_folder
                    SET local_folder_id = @local_folder_id
                    WHERE bookmark_id = @bookmark_id;
                ");

                query.AddParameter("@bookmark_id", bookmarkId);
                query.AddParameter("@local_folder_id", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                if (GetFolderByLocalId(c, localFolderId) == null)
                {
                    throw new FolderNotFoundException(localFolderId);
                }

                if (GetBookmarkByIdAsync(c, bookmarkId) == null)
                {
                    throw new BookmarkNotFoundException(bookmarkId);
                }

                MoveBookmarkToFolder();
            });
        }

        public Task DeleteBookmarkAsync(long bookmarkId)
        {
            var c = this.connection;

            void RemoveFromFolder()
            {
                using var query = c!.CreateCommand(@"
                    DELETE FROM bookmark_to_folder
                    WHERE bookmark_id = @bookmark_id
                ");

                query.AddParameter("@bookmark_id", bookmarkId);

                query.ExecuteNonQuery();
            }

            void DeleteBookmark()
            {
                using var query = c!.CreateCommand(@"
                    DELETE FROM bookmarks
                    WHERE id = @id
                ");

                query.AddParameter("@id", bookmarkId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                RemoveFromFolder();
                DeleteBookmark();
            });
        }
    }
}