using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Bookmark information that is held only locally, and not round tripped
    /// to the service
    /// </summary>
    public sealed record DatabaseLocalOnlyBookmarkState
    {
        /// <summary>
        /// The ID of the bookmark this information is for
        /// </summary>
        public long BookmarkId { get; init; }

        /// <summary>
        /// Has the article been downloaded successfully, and available
        /// via local storage
        /// </summary>
        public bool AvailableLocally { get; init; } = false;

        /// <summary>
        /// Local path of any downloaded images for this bookmark
        /// </summary>
        public Uri? FirstImageLocalPath { get; init; }

        /// <summary>
        /// Remote URL of the first image referenced in the bookmark
        /// </summary>
        public Uri? FirstImageRemoteUri { get; init; }

        /// <summary>
        /// Local path for the downloaded bookmark content
        /// </summary>
        public Uri? LocalPath { get; init; }

        /// <summary>
        /// Description from the article that has been extracted from the
        /// downloaded bookmark content
        /// </summary>
        public string ExtractedDescription { get; init; } = String.Empty;

        /// <summary>
        /// Bookmark content has been marked unavailable by the Instapaper
        /// service, and shouldn't be considered for downloading again.
        /// </summary>
        public bool ArticleUnavailable { get; init; } = false;

        /// <summary>
        /// Should viewing this article be considered for inclusion in an MRU
        /// list
        /// </summary>
        public bool IncludeInMRU { get; init; } = false;

        /// <summary>
        /// Does this article have an image available
        /// </summary>
        public bool HasImages => FirstImageRemoteUri != null;

        /// <summary>
        /// Converts a raw database row into a hydrated instance of local-only
        /// information for this bookmark.
        /// </summary>
        /// <param name="row">Row to read local-only data from</param>
        /// <returns>
        /// Instance of the local-only information for this row
        /// </returns>
        internal static DatabaseLocalOnlyBookmarkState FromRow(IDataReader row)
        {
            var bookmarkId = row.GetInt64("bookmark_id");
            var availableLocally = row.GetBoolean("available_locally");
            var firstImageLocalPath = row.GetNullableUri("first_image_local_path");
            var firstImageRemotePath = row.GetNullableUri("first_image_remote_path");
            var localPath = row.GetNullableUri("local_path");
            var extractedDescription = row.GetString("extracted_description");
            var articleUnavailable = row.GetBoolean("article_unavailable");
            var includeInMRU = row.GetBoolean("include_in_mru");

            return new DatabaseLocalOnlyBookmarkState()
            {
                BookmarkId = bookmarkId,
                AvailableLocally = availableLocally,
                FirstImageLocalPath = firstImageLocalPath,
                FirstImageRemoteUri = firstImageRemotePath,
                LocalPath = localPath,
                ExtractedDescription = extractedDescription,
                ArticleUnavailable = articleUnavailable,
                IncludeInMRU = includeInMRU
            };
        }
    }
}
