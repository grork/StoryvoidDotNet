using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Bookmark information that is held only locally, and not round tripped
    /// to the service
    /// </summary>
    public sealed class DatabaseLocalOnlyBookmarkState
    {
        private DatabaseLocalOnlyBookmarkState()
        {
        }

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
            return new DatabaseLocalOnlyBookmarkState();
        }
    }
}
