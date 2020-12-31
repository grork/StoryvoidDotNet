using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Bookmark sourced from the Database
    /// </summary>
    public sealed record DatabaseBookmark
    {
        private DatabaseBookmark() { }

        /// <summary>
        /// Local-only information for this bookmark, if present.
        /// </summary>
        public DatabaseLocalOnlyBookmarkState? LocalOnlyState { get; init; }

        /// <summary>
        /// Convenience access to check if have local-only state information
        /// available.
        /// </summary>
        public bool HasLocalState => this.LocalOnlyState != null;

        /// <summary>
        /// Bookmark ID in the local database, and on the service
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// URL that this bookmark represents
        /// </summary>
        public Uri Url { get; init; } = new Uri("unset://unset");

        /// <summary>
        /// Display title for this bookmark
        /// </summary>
        public string Title { get; init; } = String.Empty;

        /// <summary>
        /// Optional description of the bookmark
        /// </summary>
        public string Description { get; init; } = String.Empty;

        /// <summary>
        /// Current read progress of the bookmark -- between 0.0 and 1.0
        /// </summary>
        public float ReadProgress { get; init; }

        /// <summary>
        /// Time that the progress was last changed
        /// </summary>
        public DateTime ReadProgressTimestamp { get; init; }

        /// <summary>
        /// Hash provided by the service of the bookmark reading progress &amp;
        /// change timestamp.
        /// </summary>
        public string Hash { get; init; } = String.Empty;

        /// <summary>
        /// Has this bookmark been liked
        /// </summary>
        public bool Liked { get; init; }

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
            var description = String.Empty;
            DatabaseLocalOnlyBookmarkState? localOnlyState = null;

            if (!row.IsDBNull("description"))
            {
                description = row.GetString("description");
            }

            // If there is an associated bookmark ID, this implies that there is
            // download / local state.
            if (!row.IsDBNull("bookmark_id"))
            {
                localOnlyState = DatabaseLocalOnlyBookmarkState.FromRow(row);
            }

            var bookmark = new DatabaseBookmark()
            {
                Id = id,
                Url = url,
                Title = title,
                ReadProgress = progress,
                ReadProgressTimestamp = progressTimestamp,
                Hash = hash,
                Liked = liked,
                Description = description,
                LocalOnlyState = localOnlyState
            };

            return bookmark;
        }
    }
}
