using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Pending Folder Add sourced from the Database
    /// </summary>
    public sealed record PendingFolderAdd
    {
        private PendingFolderAdd() { }

        /// <summary>
        /// ID for the pending change itself, so that it can be deleted later.
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// ID of the folder that has been added, so additional information may
        /// be later retrieved about that folder
        /// </summary>
        public long FolderLocalId { get; init; }

        /// <summary>
        /// Title of the folder that has been added
        /// </summary>
        public string Title { get; init; } = String.Empty;

        /// <summary>
        /// Converts a raw database row into a hydrated Pending Folder Add
        /// instance
        /// </summary>
        /// <param name="row">Row to read Pending Folder Add data from</param>
        /// <returns>Instance of a Pending Folder Add</returns>
        internal static PendingFolderAdd FromRow(IDataReader row)
        {
            var changeId = row.GetInt64("change_id");
            var folderLocalId = row.GetInt64("local_id");
            var title = row.GetString("title")!;

            var change = new PendingFolderAdd()
            {
                Id = changeId,
                FolderLocalId = folderLocalId,
                Title = title
            };

            return change;
        }
    }

    /// <summary>
    /// Pending Folder Delete sourced from the database
    /// </summary>
    public sealed record PendingFolderDelete
    {
        private PendingFolderDelete() { }

        /// <summary>
        /// ID for the pending change itself, so that it can be deleted later.
        /// </summary>
        public long ChangeId { get; init; }

        /// <summary>
        /// Service ID for the folder that is being deleted.
        /// </summary>
        public long ServiceId { get; init; }

        /// <summary>
        /// Title of the folder that was deleted, so that if the folder is
        /// readded locally we can resurrect the folder w/ the same service ID
        /// </summary>
        public string Title { get; init; } = String.Empty;

        /// <summary>
        /// Converts a raw database row into a hydrated Pending Folder Delete
        /// </summary>
        /// <param name="row">Row to read Pending Folder Delete from</param>
        /// <returns></returns>
        internal static PendingFolderDelete FromRow(IDataReader row)
        {
            var changeId = row.GetInt64("change_id");
            var serviceId = row.GetInt64("service_id");
            var title = row.GetString("title")!;

            var change = new PendingFolderDelete()
            {
                ChangeId = changeId,
                ServiceId = serviceId,
                Title = title
            };

            return change;
        }
    }
}