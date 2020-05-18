using System;
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
        double Progress { get; }
        DateTime ProgressTimestamp { get; }
        bool Liked { get; }
        string Hash { get; }
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
        /// <param name="folderId">Wellknown Folder to list bookmarks for</param>
        /// <param name="haveInformation"><see cref="HaveStatus"/>'s for the
        /// known bookmarks &amp; state for the requested folder
        /// </param>
        /// <returns>
        /// List of bookmarks
        /// </returns>
        Task<IList<IBookmark>> List(string folderId, IEnumerable<HaveStatus>? haveInformation);

        /// <summary>
        /// List the bookmarks for a specific folder. For wellknown folders
        /// <see cref="WellKnownFolderIds"/>. Other folders can be listed using
        /// the folder ID from the <see cref="FoldersClient"/> API
        /// </summary>
        /// <param name="folderId">Folder Id to list bookmarks for</param>
        /// <param name="haveInformation"><see cref="HaveStatus"/>'s for the
        /// known bookmarks &amp; state for the requested folder
        /// </param>
        /// <returns>
        /// List of bookmarks
        /// </returns>
        Task<IList<IBookmark>> List(ulong folderId, IEnumerable<HaveStatus>? haveInformation);

        /// <summary>
        /// Add a bookmark for the supplied URL.
        /// </summary>
        /// <param name="bookmarkUrl">URL to add</param>
        /// <returns>The bookmark if successf</returns>
        Task<IBookmark> Add(Uri bookmarkUrl);

        /// <summary>
        /// Updates the progress of a specific bookmark, explicitly.
        /// </summary>
        /// <param name="bookmark_id">Bookmark to update</param>
        /// <param name="progress">The progress, between 0.0 and 1.0</param>
        /// <param name="progress_timestamp">Time when progress was changed</param>
        /// <returns>The bookmark as returned by the service after the update</returns>
        Task<IBookmark> UpdateReadProgress(ulong bookmark_id, double progress, DateTime progress_timestamp);

        /// <summary>
        /// Sets the state of a bookmark to be 'Liked', irrespective of it's
        /// current like state
        /// </summary>
        /// <param name="bookmark_id">Bookmark to like</param>
        /// <returns>The updated bookmark</returns>
        Task<IBookmark> Like(ulong bookmark_id);

        /// <summary>
        /// Sets the state of a bookmark to be 'unliked', irrespective of it's
        /// current like state
        /// </summary>
        /// <param name="id">Bookmark to unlike</param>
        /// <returns>The updated bookmark</returns>
        Task<IBookmark> Unlike(ulong bookmark_id);

        /// <summary>
        /// Archives a bookmark -- which moves it out of the unread folder and
        /// into the well known 'Archive' folder
        /// </summary>
        /// <param name="id">Bookmark to archive</param>
        /// <returns>The updated bookmark</returns>
        Task<IBookmark> Archive(ulong bookmark_id);

        /// <summary>
        /// Unarchives a bookmark -- which moves it to the well known 'unread'
        /// folder, and is no longer in the 'Archive' folder
        /// </summary>
        /// <param name="id"></param>
        /// <returns>The updated bookmark</returns>
        Task<IBookmark> Unarchive(ulong bookmark_id);

        /// <summary>
        /// Moves the supplied bookmark to the supplied folder.
        /// </summary>
        /// <param name="bookmark_id">Bookmark to move</param>
        /// <param name="folder_id">Folder to move the bookmark to</param>
        /// <returns>Bookmark after completing the move</returns>
        Task<IBookmark> Move(ulong bookmark_id, ulong folder_id);
    }

    public static class IBookmarksClientExtension
    {
        /// <summary>
        /// List the bookmarks for a specific folder. For wellknown folders
        /// <see cref="WellKnownFolderIds"/>. Other folders can be listed using
        /// the folder ID from the <see cref="FoldersClient"/> API
        /// </summary>
        /// <param name="folderId">Wellknown Folder to list bookmarks for</param>
        /// <returns>
        /// List of bookmarks
        /// </returns>
        public static Task<IList<IBookmark>> List(this IBookmarksClient instance, string wellKnownFolderId)
        {
            return instance.List(wellKnownFolderId, null);
        }

        /// <summary>
        /// List the bookmarks for a specific folder. Other folders can be listed using
        /// the folder ID from the <see cref="FoldersClient"/> API
        /// </summary>
        /// <param name="folderId">Folder Id to list bookmarks for</param>
        /// <returns>
        /// List of bookmarks
        /// </returns>
        public static Task<IList<IBookmark>> List(this IBookmarksClient instance, ulong folder_id)
        {
            return instance.List(folder_id, null);
        }
    }

    internal class Bookmark : IBookmark
    {
        internal static IBookmark FromJsonElement(JsonElement bookmarkElement)
        {
            var urlString = bookmarkElement.GetProperty("url").GetString();
            var url = new Uri(urlString);
            var id = bookmarkElement.GetProperty("bookmark_id").GetUInt64();
            var likedRaw = bookmarkElement.GetProperty("starred").GetString();
            var liked = (likedRaw == "1");

            // Progress
            var progress = bookmarkElement.GetProperty("progress").GetDouble();
            var progressTimestampRaw = bookmarkElement.GetProperty("progress_timestamp").GetInt64();
            var progressTimestampUnixEpoch = DateTimeOffset.FromUnixTimeMilliseconds(progressTimestampRaw);
            var progressTimestamp = progressTimestampUnixEpoch.LocalDateTime;

            // Hash
            var hash = bookmarkElement.GetProperty("hash").GetString();

            return new Bookmark()
            {
                Url = url,
                Id = id,
                Progress = progress,
                ProgressTimestamp = progressTimestamp,
                Liked = liked,
                Hash = hash
            };
        }

        public Uri Url { get; private set; } = new Uri("unset://unset");
        public ulong Id { get; private set; } = 0L;
        public double Progress { get; private set; } = 0.0;
        public DateTime ProgressTimestamp { get; private set; } = DateTime.MinValue;
        public bool Liked { get; private set; } = false;
        public string Hash { get; private set; } = String.Empty;
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
        /// <param name="bookmark_id">Bookmark ID</param>
        /// <param name="hash">
        ///     Hash of the last status (generated by the service, not locally)
        /// </param>
        /// <param name="readProgress">
        ///     Amount of bookmark that has been read, between 0.0 and 1.0
        /// </param>
        /// <param name="changed">Last time the progress changed</param>
        public HaveStatus(ulong bookmark_id, string hash, double readProgress, DateTime changed) : this(bookmark_id, hash)
        {
            this.ReadProgress = readProgress;
            this.Changed = changed;
        }

        /// <summary>
        /// When you only have -- or only want to use -- the ID information
        /// </summary>
        /// <param name="id">Bookmark ID</param>
        public HaveStatus(ulong bookmark_id)
        {
            if (bookmark_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
            }

            this.Id = bookmark_id;
            this.Hash = String.Empty;
            this.ReadProgress = null;
            this.Changed = null;
        }

        /// <summary>
        /// With an ID & hash, you can use this constructor
        /// </summary>
        /// <param name="bookmark_id"></param>
        /// <param name="hash"></param>
        public HaveStatus(ulong bookmark_id, string hash) : this(bookmark_id)
        {
            if (String.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentOutOfRangeException(nameof(hash), "Hash required for this bookmark");
            }

            this.Hash = hash;
        }

        /// <summary>
        /// ID of the bookmark, as originally from the service
        /// </summary>
        public ulong Id { get; }

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
        public DateTime? Changed { get; }

        /// <summary>
        /// The 'have' syntax is a very specific string, so overriding it here
        /// allows us to format it correctly. For more details on this format
        /// see <see href="https://www.instapaper.com/api">here</see>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder haveText = new StringBuilder(this.Id.ToString());

            if (!String.IsNullOrWhiteSpace(this.Hash))
            {
                haveText.AppendFormat(":{0}", this.Hash);
            }

            if (this.ReadProgress != null && this.Changed != null)
            {
                haveText.AppendFormat(":{0}:{1}", this.ReadProgress, new DateTimeOffset(this.Changed.Value).ToUnixTimeMilliseconds());
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

        private Task<IList<IBookmark>> PerformRequestAsync(Uri endpoint, string parameterName, string parameterValue)
        {
            var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>(parameterName, parameterValue) });
            return this.PerformRequestAsync(endpoint, content);
        }

        private Task<IList<IBookmark>> PerformRequestAsync(Uri endpoint, IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var content = new FormUrlEncodedContent(parameters);
            return this.PerformRequestAsync(endpoint, content);
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

        /// <summary>
        /// Makes request, based only on the bookmark ID, and expects to only to
        /// return a single bookmark as the result.
        /// </summary>
        /// <param name="endpoint">URI to post the data to</param>
        /// <param name="bookmark_id">The bookmark to operate on</param>
        /// <returns>Single bookmark on success</returns>
        private async Task<IBookmark> SingleBookmarkOperation(Uri endpoint, ulong bookmark_id)
        {
            if (bookmark_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid bookmark");
            }

            var result = await this.PerformRequestAsync(endpoint, "bookmark_id", bookmark_id.ToString());

            Debug.Assert(result.Count == 1, $"Expected one bookmark in result, {result.Count} found");
            return result.First();
        }

        public Task<IList<IBookmark>> List(string wellKnownFolderId, IEnumerable<HaveStatus>? haveInformation)
        {
            var parameters = new Dictionary<string, string>();

            if (!String.IsNullOrWhiteSpace(wellKnownFolderId))
            {
                parameters.Add("folder_id", wellKnownFolderId);
            }

            if (haveInformation != null)
            {
                var havePayload = String.Join(",", haveInformation);
                if (!String.IsNullOrWhiteSpace(havePayload))
                {
                    parameters.Add("have", havePayload);
                }
            }

            if (parameters.Count == 0)
            {
                return this.PerformRequestAsync(EndPoints.Bookmarks.List, new StringContent(String.Empty));
            }

            return this.PerformRequestAsync(EndPoints.Bookmarks.List, parameters);
        }

        public Task<IList<IBookmark>> List(ulong folder_id, IEnumerable<HaveStatus>? haveInformation)
        {
            if (folder_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(folder_id), "Invalid Folder ID");
            }

            return this.List(folder_id.ToString(), haveInformation);
        }

        public async Task<IBookmark> Add(Uri bookmarkUrl)
        {
            if ((bookmarkUrl.Scheme != Uri.UriSchemeHttp)
                && (bookmarkUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Only HTTP or HTTPS Urls are supported");
            }

            var result = await this.PerformRequestAsync(EndPoints.Bookmarks.Add, "url", bookmarkUrl.ToString());

            Debug.Assert(result.Count == 1, $"Expected one bookmark added, {result.Count} found");
            return result.First();
        }

        public async Task<IBookmark> UpdateReadProgress(ulong bookmark_id, double progress, DateTime progress_timestamp)
        {
            if (bookmark_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid bookmark");
            }

            if (progress < 0.0 || progress > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress for a bookmark must be between 0.0 and 1.0");
            }

            var progressInUnixEpoch = new DateTimeOffset(progress_timestamp).ToUnixTimeMilliseconds();
            if (progressInUnixEpoch < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(progress_timestamp), "Progress was before Unix epoch");
            }

            var parameters = new Dictionary<string, string>()
            {
                { "bookmark_id", bookmark_id.ToString() },
                { "progress", progress.ToString() },
                { "progress_timestamp", progressInUnixEpoch.ToString() }
            };

            var result = await this.PerformRequestAsync(EndPoints.Bookmarks.UpdateReadProgress, parameters);

            Debug.Assert(result.Count == 1, $"Expected one bookmark progress updated, {result.Count} found");
            return result.First();
        }

        public Task<IBookmark> Like(ulong bookmark_id) => this.SingleBookmarkOperation(EndPoints.Bookmarks.Star, bookmark_id);
        public Task<IBookmark> Unlike(ulong bookmark_id) => this.SingleBookmarkOperation(EndPoints.Bookmarks.Unstar, bookmark_id);
        public Task<IBookmark> Archive(ulong bookmark_id) => this.SingleBookmarkOperation(EndPoints.Bookmarks.Archive, bookmark_id);
        public Task<IBookmark> Unarchive(ulong bookmark_id) => this.SingleBookmarkOperation(EndPoints.Bookmarks.Unarchive, bookmark_id);

        public async Task<IBookmark> Move(ulong bookmark_id, ulong folder_id)
        {
            if (bookmark_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
            }

            if (folder_id == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(folder_id), "Invalid Folder ID");
            }

            var parameters = new Dictionary<string, string>()
            {
                { "bookmark_id", bookmark_id.ToString() },
                { "folder_id", folder_id.ToString() }
            };

            var result = await this.PerformRequestAsync(EndPoints.Bookmarks.Move, parameters);

            Debug.Assert(result.Count == 1, $"Expected one bookmark to be moved, {result.Count} found");
            return result.First();
        }
    }
}