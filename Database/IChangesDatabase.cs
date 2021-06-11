using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Manipulate changes that have been (or should be) performed on the
    /// database so that syncing can replay those changes at a later date
    /// </summary>
    public interface IChangesDatabase
    {
        /// <summary>
        /// Creates a pending folder addition in the changes database
        /// </summary>
        /// <param name="localFolderId">
        /// The already-created local folder ID to create a pending change for
        /// </param>
        /// <returns>Instance representing the pending folder add</returns>
        PendingFolderAdd CreatePendingFolderAdd(long localFolderId);

        /// <summary>
        /// Removes a pending folder add from the changes database. If no change
        /// with that ID is present, it completes silently.
        /// </summary>
        /// <param name="changeId">Pending folder add to remove</param>
        void RemovePendingFolderAdd(long changeId);

        /// <summary>
        /// Gets a specific pending folder addition by the change ID for that
        /// change, if present in the database.
        /// </summary>
        /// <param name="changeId">Change ID for that folder add</param>
        /// <returns>Change for the supplied ID</returns>
        PendingFolderAdd? GetPendingFolderAdd(long changeId);

        /// <summary>
        /// Gets a pending folder add by the local folder ID if it exists.
        /// </summary>
        /// <param name="localFolderId">Local folder ID to search for</param>
        /// <returns>Pending folder add if found</returns>
        PendingFolderAdd? GetPendingFolderAddByLocalFolderId(long localFolderId);

        /// <summary>
        /// Returns all pending folder additions
        /// </summary>
        /// <returns>Unordered list of folders that have been added</returns>
        IList<PendingFolderAdd> ListPendingFolderAdds();

        /// <summary>
        /// Create a pending folder delete in the changes database
        /// </summary>
        /// <param name="serviceId">The service ID for this folder</param>
        /// <param name="title">Title at the time of deleition</param>
        /// <returns>Instance representing the pending folder delete</returns>
        PendingFolderDelete CreatePendingFolderDelete(long serviceId, string title);

        /// <summary>
        /// Removes a pending folder delete from the changes database. If no
        /// change with that is present, it completes silently
        /// </summary>
        /// <param name="changeId">Pending folder delete to remove </param>
        void RemovePendingFolderDelete(long changeId);

        /// <summary>
        /// Get a specific pending folder delete by the change ID for that
        /// change, if present in the database
        /// </summary>
        /// <param name="changeId">Change ID for that folder delete</param>
        /// <returns>A pending folder delete instance if foudn</returns>
        PendingFolderDelete? GetPendingFolderDelete(long changeId);

        /// <summary>
        /// Returns all pending folder deletes
        /// </summary>
        /// <returns>Unordered list of folders that have been deleted</returns>
        IList<PendingFolderDelete> ListPendingFolderDeletes();
    }
}
