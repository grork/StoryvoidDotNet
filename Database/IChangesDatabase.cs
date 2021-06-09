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
        /// <returns>Complete pending folder add</returns>
        PendingFolderAdd CreatePendingFolderAdd(long localFolderId);

        /// <summary>
        /// Gets a specific pending folder addition by the change ID for that
        /// change
        /// </summary>
        /// <param name="changeId">Change ID for that folder</param>
        /// <returns>Change for the supplied ID</returns>
        Task<PendingFolderAdd?> GetPendingFolderAddAsync(long changeId);

        /// <summary>
        /// Returns all pending folder additions
        /// </summary>
        /// <returns>Unordered list of folders that have been added</returns>
        Task<IList<PendingFolderAdd>> ListPendingFolderAddsAsync();
    }
}
