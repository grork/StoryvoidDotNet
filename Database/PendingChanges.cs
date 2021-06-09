using System;
using System.Data;

namespace Codevoid.Storyvoid
{
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
}