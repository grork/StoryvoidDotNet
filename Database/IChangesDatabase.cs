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
        /// Gets a specific pending folder addition by the change ID for that
        /// change, if present in the database.
        /// </summary>
        /// <param name="changeId">Change ID for that folder add</param>
        /// <returns>Change for the supplied ID</returns>
        Task<PendingFolderAdd?> GetPendingFolderAddAsync(long changeId);

        /// <summary>
        /// Returns all pending folder additions
        /// </summary>
        /// <returns>Unordered list of folders that have been added</returns>
        Task<IList<PendingFolderAdd>> ListPendingFolderAddsAsync();

        /// <summary>
        /// Create a pending folder delete in the changes database
        /// </summary>
        /// <param name="serviceId">The service ID for this folder</param>
        /// <param name="title">Title at the time of deleition</param>
        /// <returns>Instance representing the pending folder delete</returns>
        PendingFolderDelete CreatePendingFolderDelete(long serviceId, string title);

        /// <summary>
        /// Get a specific pending folder delete by the change ID for that
        /// change, if present in the database
        /// </summary>
        /// <param name="changeId">Change ID for that folder delete</param>
        /// <returns>A pending folder delete instance if foudn</returns>
        Task<PendingFolderDelete?> GetPendingFolderDeleteAsync(long changeId);

        /// <summary>
        /// Returns all pending folder deletes
        /// </summary>
        /// <returns>Unordered list of folders that have been deleted</returns>
        Task<IList<PendingFolderDelete>> ListPendingFolderDeletesAsync();
    }
}
