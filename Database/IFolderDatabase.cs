using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// For accessing well known folders from the service that don't have
    /// explicit service IDs.
    /// </summary>
    public static class WellKnownServiceFolderIds
    {
        /// <summary>
        /// Default folder on the service, where new articles are placed by
        /// default.
        /// </summary>
        public const long Unread = -1;

        /// <summary>
        /// Folder for articles that have been archived by the user.
        /// </summary>
        public const long Archive = -2;
    }

    /// <summary>
    /// For accessing well known folders in the local database that are always
    /// guarenteed to exist.
    /// </summary>
    public static class WellKnownLocalFolderIds
    {
        /// <summary>
        /// Default folder, where new articles are placed default.
        /// </summary>
        public const long Unread = 1;

        /// <summary>
        /// Folder for articles that have been archived by the user.
        /// </summary>
        public const long Archive = 2;
    }

    /// <summary>
    /// Manage folders in the local Instapaper Database
    /// </summary>
    public interface IFolderDatabase
    {
        /// <summary>
        /// Gets all locally known folders, including the default
        /// folders (Unread, Archive)
        /// </summary>
        /// <returns>List of folders</returns>
        IList<DatabaseFolder> ListAllFolders();

        /// <summary>
        /// Gets a specific folder using it's service ID
        /// </summary>
        /// <param name="serviceId">Service ID of the folder</param>
        /// <returns>Folder if found, null otherwise</returns>
        DatabaseFolder? GetFolderByServiceId(long serviceId);

        /// <summary>
        /// Gets a specific folder using it's local ID
        /// </summary>
        /// <param name="serviceId">Local ID of the folder</param>
        /// <returns>Folder if found, null otherwise</returns>
        DatabaseFolder? GetFolderByLocalId(long localId);

        /// <summary>
        /// Creates a new, local folder
        /// </summary>
        /// <param name="title">Title of the folder</param>
        /// <returns>A new folder instance</returns>
        DatabaseFolder CreateFolder(string title);

        /// <summary>
        /// Adds a known folder to the database. This intended to be used when
        /// you have a fully-filled out folder from the service.
        /// </summary>
        /// <param name="title">Title for this folder</param>
        /// <param name="serviceId">The ID of the folder on the service</param>
        /// <param name="position">The relative order of the folder</param>
        /// <param name="shouldSync">Should the folder by synced</param>
        /// <returns>The folder after being added to the database</returns>
        DatabaseFolder AddKnownFolder(string title, long serviceId, long position, bool shouldSync);

        /// <summary>
        /// Updates the data of a folder with the supplied Local ID. All fields
        /// must be supplied.
        ///
        /// NB: This API is only intended to be used to update local information
        /// directly sourced from the server. Changed properties of this field
        /// will not be round-tripped to the service.
        /// </summary>
        /// <param name="localId">Item to update</param>
        /// <param name="serviceId">Service ID to set</param>
        /// <param name="title">Title to set</param>
        /// <param name="position">Position to set</param>
        /// <param name="shouldSync">Should be synced</param>
        /// <returns>Updated folder</returns>
        DatabaseFolder UpdateFolder(long localId, long? serviceId, string title, long position, bool shouldSync);

        /// <summary>
        /// Delete the specified folder. Any articles in this folder will be
        /// orphaned until they're reconciled against the server, or othewise
        /// removed.
        /// </summary>
        /// <param name="localFolderId">Folder to delete</param>
        void DeleteFolder(long localFolderId);
    }
}