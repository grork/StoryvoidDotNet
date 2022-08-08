namespace Codevoid.Storyvoid;

/// <summary>
/// Manipulate folder changes that have been (or should be) performed on the
/// database so that syncing can replay those changes at a later date
/// </summary>
public interface IFolderChangesDatabase
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
    /// Deletes a pending folder add from the changes database. If no change
    /// with that ID is present, it completes silently.
    /// </summary>
    /// <param name="localFolderId">Pending folder add to delete</param>
    void DeletePendingFolderAdd(long localFolderId);

    /// <summary>
    /// Gets a pending folder add by the local folder ID if it exists.
    /// </summary>
    /// <param name="localFolderId">Local folder ID to search for</param>
    /// <returns>Pending folder add if found</returns>
    PendingFolderAdd? GetPendingFolderAdd(long localFolderId);

    /// <summary>
    /// Returns all pending folder additions
    /// </summary>
    /// <returns>Unordered list of folders that have been added</returns>
    IEnumerable<PendingFolderAdd> ListPendingFolderAdds();

    /// <summary>
    /// Create a pending folder delete in the changes database
    /// </summary>
    /// <param name="serviceId">The service ID for this folder</param>
    /// <param name="title">Title at the time of deleition</param>
    /// <returns>Instance representing the pending folder delete</returns>
    PendingFolderDelete CreatePendingFolderDelete(long serviceId, string title);

    /// <summary>
    /// Delete a pending folder delete from the changes database. If no
    /// change with that is present, it completes silently
    /// </summary>
    /// <param name="serviceId">
    /// Service ID of folder delete to delete the pending change for
    /// </param>
    void DeletePendingFolderDelete(long serviceId);

    /// <summary>
    /// Get a specific pending folder delete by the change ID for that
    /// change, if present in the database
    /// </summary>
    /// <param name="serviceId">Service ID for that folder delete</param>
    /// <returns>A pending folder delete instance if found</returns>
    PendingFolderDelete? GetPendingFolderDelete(long serviceId);

    /// <summary>
    /// Get a specific pending folder delete by the title of the deleted folded
    /// that change represents.
    /// 
    /// This is intended to help address stacked delete/add scenarios that occur
    /// between syncs, and allows us to resurrect the deleted folder with the
    /// original service ID.
    /// </summary>
    /// <param name="title">Title of the folder that was deleted</param>
    /// <returns>A pending folder delete instance if found</returns>
    PendingFolderDelete? GetPendingFolderDeleteByTitle(string title);

    /// <summary>
    /// Returns all pending folder deletes
    /// </summary>
    /// <returns>Unordered list of folders that have been deleted</returns>
    IEnumerable<PendingFolderDelete> ListPendingFolderDeletes();
}