﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Instapaper
{
    /// <summary>
    /// A Bookmark from the service
    /// </summary>
    public interface IBookmark
    {
        Uri Url { get; }
        ulong Id { get; }
    }

    /// <summary>
    /// Client for manipulating Bookmarks on the Instapaper Service
    /// </summary>
    public interface IBookmarksClient
    {
        /// <summary>
        /// List the bookmarks for a specific folder. For wellknown folders
        /// <see cref="WellKnownFolderIds"/>. Other folders can be listed using
        /// the folder ID from the <see cref="FoldersClient"/> API
        /// </summary>
        /// <param name="folderId">Folder ID to list bookmarks for</param>
        /// <returns>
        /// List of bookmarks
        /// </returns>
        Task<IList<IBookmark>> List(string folderId);

        /// <summary>
        /// Add a bookmark for the supplied URL.
        /// </summary>
        /// <param name="bookmarkUrl">URL to add</param>
        /// <returns>The bookmark if successf</returns>
        Task<IBookmark> Add(Uri bookmarkUrl);
    }

    internal class Bookmark : IBookmark
    {
        internal static IBookmark FromJsonElement(JsonElement bookmarkElement)
        {
            var urlString = bookmarkElement.GetProperty("url").GetString();
            var url = new Uri(urlString);
            var id = bookmarkElement.GetProperty("bookmark_id").GetUInt64();
            return new Bookmark()
            {
                Url = url,
                Id = id
            };
        }

        public Uri Url { get; private set; } = new Uri("unset://unset");
        public ulong Id { get; private set; } = 0L;
    }

    /// <summary>
    /// Lightweight information about the status of a bookmark for syncing
    /// purposes. Encapsulates having the bookmark & it's state, as well as
    /// progress & progress changed information
    /// </summary>
    public readonly struct HaveStatus
    {
        /// <summary>
        ///     When you have all peices of the bookmark state, this constructor
        ///     encapsulates that information.
        /// </summary>
        /// <param name="id">Bookmark ID</param>
        /// <param name="hash">
        ///     Hash of the last status (generated by the service, not locally)
        /// </param>
        /// <param name="readProgress">
        ///     Amount of bookmark that has been read, between 0.0 and 1.0
        /// </param>
        /// <param name="changed">Last time the progress changed</param>
        public HaveStatus(string id, string hash, double readProgress, DateTimeOffset changed) : this(id, hash)
        {
            this.ReadProgress = readProgress;
            this.Changed = changed;
        }

        /// <summary>
        /// When you only have -- or only want to use -- the ID information
        /// </summary>
        /// <param name="id">Bookmark ID</param>
        public HaveStatus(string id)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id", "id of the bookmark is required");
            }

            this.Id = id;
            this.Hash = String.Empty;
            this.ReadProgress = null;
            this.Changed = null;
        }

        /// <summary>
        /// With an ID & hash, you can use this constructor
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        public HaveStatus(string id, string hash) : this(id)
        {
            if (String.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentNullException("hash", "Hash required for this bookmark");
            }

            this.Hash = hash;
        }

        /// <summary>
        /// ID of the bookmark, as originally from the service
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Service provided hash of the bookmark state. Can't be derived locally
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// The read progress of this bookmark as a percentage from 0.0 to 1.0
        /// </summary>
        public double? ReadProgress { get; }

        /// <summary>
        /// The unix epoch time that the progress was last updated.
        /// </summary>
        public DateTimeOffset? Changed { get; }

        /// <summary>
        /// The 'have' syntax is a very specific string, so overriding it here
        /// allows us to format it correctly. For more details on this format
        /// see <see href="https://www.instapaper.com/api">here</see>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder haveText = new StringBuilder(this.Id);

            if (!String.IsNullOrWhiteSpace(this.Hash))
            {
                haveText.AppendFormat(":{0}", this.Hash);
            }

            if (this.ReadProgress != null && this.Changed != null)
            {
                haveText.AppendFormat(":{0}:{1}", this.ReadProgress, this.Changed?.ToUnixTimeSeconds());
            }

            return haveText.ToString();
        }
    }

    /// <summary>
    /// Bookmark operations for Instapaper -- adding removing, changing states
    /// </summary>
    public class BookmarksClient : IBookmarksClient
    {
        private readonly HttpClient client;

        public BookmarksClient(ClientInformation clientInformation)
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

        private async Task<IList<IBookmark>> PerformRequestAsync(Uri endpoint, HttpContent content)
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
            IList<IBookmark> bookmarks = new List<IBookmark>();
            foreach (var element in payload.EnumerateArray())
            {
                var itemType = element.GetProperty("type").GetString();
                switch (itemType)
                {
                    case "bookmark":
                        bookmarks.Add(Bookmark.FromJsonElement(element));
                        break;

                    case "error":
                        // Always throw an error when we encounter it, no matter
                        // if we've seen other (valid data)
                        throw ExceptionMapper.FromErrorJson(element);

                    case "user":
                    case "meta":
                        // We don't process meta or users objects
                        continue;

                    default:
                        Debug.Fail($"Unexpected return object type: {itemType}");
                        continue;
                }
            }

            return bookmarks;
        }

        public async Task<IList<IBookmark>> List(string wellKnownFolderId)
        {
            var result = await this.PerformRequestAsync(EndPoints.Bookmarks.List, new StringContent(String.Empty));
            return result;
        }

        public async Task<IBookmark> Add(Uri bookmarkUrl)
        {
            if ((bookmarkUrl.Scheme != Uri.UriSchemeHttp)
                && (bookmarkUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Only HTTP or HTTPS Urls are supported");
            }

            var result = await this.PerformRequestAsync(EndPoints.Bookmarks.Add, new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "url", bookmarkUrl.ToString() }
            }));

            Debug.Assert(result.Count == 1, $"Expected one bookmark added, {result.Count} found");
            return result.First();
        }
    }
}