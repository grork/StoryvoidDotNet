using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        string Title { get; }
    }

    /// <summary>
    /// Concrete implementation of <see cref="IFolder"/> for internal usage.
    /// </summary>
    internal class Folder : IFolder
    {
        /// <summary>
        /// Title of the folder
        /// </summary>
        public string Title { get; private set; }

        public Folder(string title)
        {
            this.Title = title;
        }
    }

    /// <summary>
    /// Client for manipulating folders on the service
    /// </summary>
    public interface IFoldersClient
    {
        /// <summary>
        /// Lists all folders (but not their contents) currently on the service
        /// </summary>
        Task<IList<IFolder>> List();

        /// <summary>
        /// Adds a folder to the service, with the supplied parameters
        /// </summary>
        /// <param name="folderTitle">Title of the folder to be added</param>
        /// <returns>Created folder from the service</returns>
        Task<IFolder> Add(string folderTitle);
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


    public class FoldersClient : IFoldersClient
    {
        private readonly HttpClient client;

        public FoldersClient(ClientInformation clientInformation)
        {
            this.client = OAuthMessageHandler.CreateOAuthHttpClient(clientInformation);
        }

        public async Task<IList<IFolder>> List()
        {
            var result = await this.client.PostAsync(Endpoints.Folders.List, new StringContent(String.Empty));
            result.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(await result.Content.ReadAsStreamAsync()).RootElement;
            Debug.Assert(JsonValueKind.Array == document.ValueKind, "Root Element was not an array");

            var folders = new List<IFolder>();
            foreach (var element in document.EnumerateArray())
            {
                var itemType = element.GetProperty("type").GetString();
                switch (itemType)
                {
                    case "meta":
                        continue;

                    case "folder":
                        var folderTitle = element.GetProperty("title").GetString();
                        folders.Add(new Folder(folderTitle));
                        break;
                }
            }

            return folders;
        }

        public async Task<IFolder> Add(string folderTitle)
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "title", folderTitle }
            });

            var result = await this.client.PostAsync(Endpoints.Folders.Add, payload);
            result.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(await result.Content.ReadAsStreamAsync()).RootElement;
            Debug.Assert(JsonValueKind.Array == document.ValueKind, "Root Element wasn't an array");

            IFolder? folder = null;
            foreach (var element in document.EnumerateArray())
            {
                var itemType = element.GetProperty("type").GetString();
                switch (itemType)
                {
                    case "meta":
                        continue;

                    case "folder":
                        var returnedFolderTitle = element.GetProperty("title").GetString();
                        folder = new Folder(returnedFolderTitle);
                        break;
                }
            }

            if (folder == null)
            {
                throw new InvalidOperationException("Folder wasn't created");
            }

            return folder;
        }
    }
}
