using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Folder sourced from the Database
    /// </summary>
    public sealed record DatabaseFolder
    {
        private DatabaseFolder()
        {
            this.Title = String.Empty;
        }

        /// <summary>
        /// The local ID for this folder, distinct from the services ID for the
        /// same folder.
        ///
        /// This is because it's possible to add folders locally, prior to sync
        /// so it needs a discrete identifier
        /// </summary>
        public long LocalId { get; init; }

        /// <summary>
        /// Services ID for the folder. If the folder is not on the service yet
        /// (e.g. was created offline), then it will be null.
        /// </summary>
        public long? ServiceId { get; init; }

        /// <summary>
        /// Display title for this folder
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// The relative position of this folder in the folder list. Folders
        /// that haven't been sync yet will have a position value of zero.
        ///
        /// Additionally, "Well Known" folders (Unread, Archive) will have
        /// negative values.
        /// </summary>
        public long Position { get; init; }

        /// <summary>
        /// Should this folder be synced, as determined by the service.
        /// </summary>
        public bool ShouldSync { get; init; }

        /// <summary>
        /// Has this folder been sync'd to the service?
        /// </summary>
        public bool IsOnService => (this.ServiceId != null);

        /// <summary>
        /// Converts a raw database row in to an instance of a folder
        /// </summary>
        /// <param name="row">Row to hydrate into a folder instance</param>
        /// <returns>Instance of the object representing the folder</returns>
        internal static DatabaseFolder FromRow(IDataReader row)
        {
            var rawShouldSync = row.GetInt64("should_sync");
            long? serviceId = null;

            // Service ID might be null so need to check if it's null before
            // requesting it from the row
            if (!row.IsDBNull("service_id"))
            {
                serviceId = row.GetInt64("service_id");
            }

            var folder = new DatabaseFolder()
            {
                Title = row.GetString("title"),
                LocalId = row.GetInt64("local_id"),
                Position = row.GetInt64("position"),
                ShouldSync = (rawShouldSync != 0),
                ServiceId = serviceId
            };

            return folder;
        }
    }
}
