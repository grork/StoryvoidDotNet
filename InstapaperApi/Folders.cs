using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Instapaper
{
    /// <summary>
    /// Folder from the service
    /// </summary>
    public interface IFolder
    {
        /// <summary>
        /// Title of the folder
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Should this folder be considered for syncing to mobile
        /// </summary>
        bool SyncToMobile { get; }

        /// <summary>
        /// Relative position of this folder compared to others. Note, this
        /// value is not guarenteed to be unique in a list of folders.
        /// </summary>
        ulong Position { get; }

        /// <summary>
        /// ID of this folder on the service
        /// </summary>
        ulong Id { get; }
    }

    /// <summary>
    /// Concrete implementation of <see cref="IFolder"/> for internal usage.
    /// </summary>
    internal class Folder : IFolder
    {
        /// <inheritdoc/>
        public string Title { get; private set; } = String.Empty;

        /// <inheritdoc/>
        public bool SyncToMobile { get; private set; } = true;

        /// <inheritdoc/>
        public ulong Position { get; private set; } = 0;

        /// <inheritdoc/>
        public ulong Id { get; private set; } = 0;

        internal static IFolder FromJsonElement(JsonElement folderElement)
        {
            var folderTitle = folderElement.GetProperty("title").GetString();

            // For reasons that are unclear, the position number from the service
            // created during an *add* operation is actually a double. (e.g. it
            // has a decimal component). This is utterly baffling, so we're going
            // to parse as a double, and then convert to a ulong.
            var positionRaw = folderElement.GetProperty("position").GetDouble();
            positionRaw = Math.Floor(positionRaw);
            var position = Convert.ToUInt64(positionRaw);

            // Sync to mobile is number in the payload, but we would like to
            // model it as boolean
            int syncToMobileRaw = folderElement.GetProperty("sync_to_mobile").GetInt32();
            var syncToMobile = (syncToMobileRaw == 1);

            var folderId = folderElement.GetProperty("folder_id").GetUInt64();
            return new Folder()
            {
                Title = folderTitle,
                SyncToMobile = syncToMobile,
                Position = position,
                Id = folderId
            };
        }
    }

    /// <summary>
    /// Access the Folders API for Instapaper
    /// </summary>
    public interface IFoldersClient
    {
        /// <summary>
        /// Lists all user folders (but not their contents) currently on the
        /// service. This means folders refrenced by <see cref="WellKnownFolderIds"/>
        /// will not be returned by this call, and must be assumed to exist.
        /// </summary>
        Task<IList<IFolder>> ListAsync();

        /// <summary>
        /// Adds a folder to the service, with the supplied parameters
        /// </summary>
        /// <param name="folderTitle">Title of the folder to be added</param>
        /// <returns>Created folder from the service</returns>
        Task<IFolder> AddAsync(string folderTitle);

        /// <summary>
        /// Delete the specified folder from the service.
        /// </summary>
        /// <param name="folderId">
        /// ID of the folder to delete. Must be greater than zero
        /// </param>
        /// <returns>Task that completes when folder is successfully deleted</returns>
        Task DeleteAsync(ulong folderId);
    }

    /// <summary>
    /// Folder IDs that are defined by the service, and always exist.
    /// </summary>
    public static class WellKnownFolderIds
    {
        public const string Unread = "unread";
        public const string Liked = "starred";
        public const string Archived = "archive";
    }

    /// <inheritdoc cref="IFoldersClient"/>
    public class FoldersClient : IFoldersClient
    {
        private readonly HttpClient client;

        public FoldersClient(ClientInformation clientInformation)
        {
            this.client = OAuthMessageHandler.CreateOAuthHttpClient(clientInformation);
        }

        /// <summary>
        /// Instapaper service returns structured errors in 400 (bad request)
        /// status codes, but not others. This hides those details from consumers
        /// of the raw http requests
        /// </summary>
        /// <param name="statusCode">Status to inspect</param>
        /// <returns>
        /// True, if this code is fatal (e.g. don't parse the body)
        /// </returns>
        private static bool IsFatalStatusCode(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.BadRequest:
                    return false;

                default:
                    return true;
            }
        }

        private async Task<IList<IFolder>> PerformRequestAsync(Uri endpoint, HttpContent content)
        {
            // Request data convert to JSON
            var result = await this.client.PostAsync(endpoint, content);
            if (!result.IsSuccessStatusCode && IsFatalStatusCode(result.StatusCode))
            {
                result.EnsureSuccessStatusCode();
            }

            var stream = await result.Content.ReadAsStreamAsync();
            var payload = JsonDocument.Parse(stream).RootElement;
            Debug.Assert(JsonValueKind.Array == payload.ValueKind, "API is always supposed to return an array as the root element");

            // Turn the JSON into strongly typed objects
            IList<IFolder> folders = new List<IFolder>();
            foreach (var element in payload.EnumerateArray())
            {
                var itemType = element.GetProperty("type").GetString();
                switch (itemType)
                {
                    case "meta":
                        // Folders API is not expected to have any meta objects
                        Debug.Fail("Unexpected meta object in folders API");
                        continue;

                    case "folder":
                        folders.Add(Folder.FromJsonElement(element));
                        break;

                    case "error":
                        // Always throw an error when we encounter it, no matter
                        // if we've seen other (valid data)
                        throw ExceptionMapper.FromErrorJson(element);
                }
            }

            return folders;
        }

        /// <inheritdoc/>
        public async Task<IList<IFolder>> ListAsync()
        {
            var folders = await this.PerformRequestAsync(EndPoints.Folders.List, new StringContent(String.Empty));
            return folders;
        }

        /// <inheritdoc/>
        public async Task<IFolder> AddAsync(string folderTitle)
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "title", folderTitle }
            });

            var folders = await this.PerformRequestAsync(EndPoints.Folders.Add, payload);
            if (folders.Count == 0)
            {
                throw new InvalidOperationException("Folder wasn't created");
            }

            Debug.Assert(folders.Count == 1, $"Expected one folder created, {folders.Count} found");
            return folders.First();
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(ulong folderId)
        {
            if (folderId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(folderId), "Folder ID must be greater than zero");
            }

            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "folder_id", folderId.ToString() }
            });

            var shouldBeEmptyFolders = await this.PerformRequestAsync(EndPoints.Folders.Delete, payload);
            if (shouldBeEmptyFolders.Count > 0)
            {
                throw new InvalidOperationException("Service did not return empty folder array for a delete");
            }

            Debug.Assert(shouldBeEmptyFolders.Count == 0, "Didn't expect any folders in response");
            return;
        }
    }
}
