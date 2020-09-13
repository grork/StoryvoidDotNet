using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Bookmark sourced from the Database
    /// </summary>
    public sealed class DatabaseBookmark
    {
        private DatabaseBookmark()
        {
            this.Title = String.Empty;
            this.Url = new Uri("unset://unset");
            this.Hash = String.Empty;
            this.Description = String.Empty;
        }

        /// <summary>
        /// Bookmark ID in the local database, and the service
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// URL that this bookmark represents
        /// </summary>
        public Uri Url { get; private set; }

        /// <summary>
        /// Display title for this bookmark
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Optional description of the bookmark
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Current read progress of the bookmark -- between 0.0 and 1.0
        /// </summary>
        public float ReadProgress { get; private set; }

        /// <summary>
        /// Time that the progress was last changed
        /// </summary>
        public DateTime ReadProgressTimestamp { get; private set; }

        /// <summary>
        /// Hash provided by the service of the bookmark reading progress &amp;
        /// change timestamp.
        /// </summary>
        public string Hash { get; private set; }

        /// <summary>
        /// Has this bookmark been liked
        /// </summary>
        public bool Liked { get; private set; }

        /// <summary>
        /// Converts a raw database row into a hydrated bookmark instance
        /// </summary>
        /// <param name="row">Row to read bookmark data from</param>
        /// <returns>Instance of the bookmark object for this row</returns>
        internal static DatabaseBookmark FromRow(IDataReader row)
        {
            var id = row.GetInt64("id");
            var url = row.GetUri("url");
            var title = row.GetString("title");
            var progress = row.GetFloat("read_progress");
            var progressTimestamp = row.GetDateTime("read_progress_timestamp");
            var hash = row.GetString("hash");
            var liked = row.GetBoolean("liked");

            var bookmark = new DatabaseBookmark()
            {
                Id = id,
                Url = url,
                Title = title,
                ReadProgress = progress,
                ReadProgressTimestamp = progressTimestamp,
                Hash = hash,
                Liked = liked
            };

            if (!row.IsDBNull("description"))
            {
                bookmark.Description = row.GetString("description");
            }

            return bookmark;
        }
    }
}
