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

        internal static IFolder FromJsonElement(JsonElement folderElement)
        {
            var folderTitle = folderElement.GetProperty("title").GetString();
            return new Folder(folderTitle);
        }
    }

    /// <summary>
    /// Access the Folders API for Instapaper
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

        /// <summary>
        /// Request
        /// </summary>
        /// <returns></returns>
        public async Task<IList<IFolder>> List()
        {
            var folders = await this.PerformRequestAsync(Endpoints.Folders.List, new StringContent(String.Empty));
            return folders;
        }

        public async Task<IFolder> Add(string folderTitle)
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "title", folderTitle }
            });

            var folders = await this.PerformRequestAsync(Endpoints.Folders.Add, payload);
            if (folders.Count == 0)
            {
                throw new InvalidOperationException("Folder wasn't created");
            }

            Debug.Assert(folders.Count == 1, $"Expected one folder created, {folders.Count} found");
            return folders.First();
        }
    }
}
