using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Codevoid.Storyvoid
{
    public sealed partial class ArticleDatabase
    {
        private static readonly DateTime UnixEpochStart = new DateTime(1970, 1, 1);

        ///<inheritdoc/>
        public Task<IList<DatabaseBookmark>> GetBookmarks(long localFolderId)
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
        public Task<IList<DatabaseBookmark>> GetLikedBookmarks()
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

        public Task<DatabaseBookmark?> GetBookmarkById(long id)
        {
            var c = this.connection;
            return Task.Run(() => GetBookmarkById(c, id));
        }

        private DatabaseBookmark? GetBookmarkById(IDbConnection connection, long id)
        {
            using var query = connection.CreateCommand("SELECT * FROM bookmarks WHERE id = @id");
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
        public Task<DatabaseBookmark> AddBookmark(
            (int id, string title, Uri url, string description, float progress, DateTime progressTimestamp, string hash, bool liked) data,
            long localFolderId
        )
        {
            var c = this.connection;

            void AddBookmark()
            {
                var query = c!.CreateCommand(@"
                    INSERT INTO bookmarks(id, title, url, description, progress, progress_timestamp, hash, liked)
                    VALUES (@id, @title, @url, @description, @progress, @progress_timestamp, @hash, @liked);

                    SELECT last_insert_rowid();
                ");

                query.AddParameter("@id", data.id);
                query.AddParameter("@title", data.title);
                query.AddParameter("@url", data.url);
                query.AddParameter("@description", data.description);
                query.AddParameter("@progress", data.progress);
                query.AddParameter("@progress_timestamp", data.progressTimestamp);
                query.AddParameter("@hash", data.hash);
                query.AddParameter("@liked", data.liked);

                query.ExecuteNonQuery();
            }

            void PairBookmarkToFolder()
            {
                var query = c!.CreateCommand(@"
                    INSERT INTO bookmark_to_folder(local_folder_id, bookmark_id)
                    VALUES (@local_folder_id, @bookmark_id);
                ");

                query.AddParameter("@bookmark_id", data.id);
                query.AddParameter("@local_folder_id", localFolderId);

                query.ExecuteNonQuery();
            }

            return Task.Run(() =>
            {
                AddBookmark();
                PairBookmarkToFolder();
                return GetBookmarkById(c, data.id)!;
            });
        }

        private static void UpdateLikeStatusForBookmark(IDbConnection c, long id, bool liked)
        {
            var query = c!.CreateCommand(@"
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
        public Task<DatabaseBookmark> LikeBookmark(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForBookmark(c, id, true);
                return GetBookmarkById(c, id)!;
            });
        }

        /// <inheritdoc/>
        public Task<DatabaseBookmark> UnlikeBookmark(long id)
        {
            this.ThrowIfNotReady();

            var c = this.connection;

            return Task.Run(() =>
            {
                UpdateLikeStatusForBookmark(c, id, false);
                return GetBookmarkById(c, id)!;
            });
        }

        public Task<DatabaseBookmark> UpdateProgressForBookmark(float progress, DateTime timestamp, long id)
        {
            if(progress < 0.0 || progress > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0.0 and 1.0");
            }

            if(timestamp < UnixEpochStart)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp), "Progress Timestamp must be within the Unix Epochs");
            }

            this.ThrowIfNotReady();

            var c = this.connection;
            void UpdateProgressForBookmark()
            {
                using var query = c!.CreateCommand(@"
                    UPDATE bookmarks
                    SET progress = @progress, progress_timestamp = @progress_timestamp
                    WHERE id = @id
                ");

                query.AddParameter("@id", id);
                query.AddParameter("@progress", progress);
                query.AddParameter("@progress_timestamp", timestamp);

                var impactedRows = query.ExecuteNonQuery();
                if(impactedRows < 1)
                {
                    throw new BookmarkNotFoundException(id);
                }
            }

            return Task.Run(() =>
            {
                UpdateProgressForBookmark();
                return GetBookmarkById(c, id)!;
            });
        }
    }
}