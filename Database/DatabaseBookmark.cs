using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    public class DatabaseBookmark
    {
        private DatabaseBookmark()
        {
            this.Title = String.Empty;
            this.Url = new Uri("unset://unset");
            this.Hash = String.Empty;
        }

        public long Id { get; private set; }
        public Uri Url { get; private set; }
        public string Title { get; private set; }
        public string? Description { get; private set; }
        public float Progress { get; private set; }
        public DateTime ProgressTimestamp { get; private set; }
        public string Hash { get; private set; }
        public bool Liked { get; private set; }

        public static DatabaseBookmark FromRow(IDataReader row)
        {
            var id = row.GetInt64("id");
            var url = row.GetUri("url");
            var title = row.GetString("title");
            var progress = row.GetFloat("progress");
            var progressTimestamp = row.GetDateTime("progress_timestamp");
            var hash = row.GetString("hash");
            var liked = row.GetBoolean("liked");

            var bookmark = new DatabaseBookmark()
            {
                Id = id,
                Url = url,
                Title = title,
                Progress = progress,
                ProgressTimestamp = progressTimestamp,
                Hash = hash,
                Liked = liked
            };

            if(!row.IsDBNull("description"))
            {
                bookmark.Description = row.GetString("description");
            }

            return bookmark;
        }
    }
}
