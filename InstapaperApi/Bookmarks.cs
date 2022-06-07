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

namespace Codevoid.Instapaper;

/// <summary>
/// A Bookmark from the service
/// </summary>
public interface IInstapaperBookmark
{
    /// <summary>
    /// Service ID for this bookmark, uniquely identifying the bookmark
    /// </summary>
    long Id { get; }

    /// <summary>
    /// URL for this bookmark
    /// </summary>
    Uri Url { get; }

    /// <summary>
    /// Title of this bookmark. May be user-set, or from the title of the
    /// the page at the bookmarks original URL.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// User specified description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Current read progress of the bookmark
    /// </summary>
    float Progress { get; }

    /// <summary>
    /// When the progress was last updated
    /// </summary>
    DateTime ProgressTimestamp { get; }

    /// <summary>
    /// Is this bookmark currently in a 'liked' state
    /// </summary>
    bool Liked { get; }

    /// <summary>
    /// Hash, from the service of when it last saw changes to the bookmark
    /// This can't be generated locally, and is only so sourced from the
    /// service. If you want to see what it is after you've made changes
    /// to the progress etc of this bookmark, make sure those changes are
    /// on the service, and then request the folder containing the bookmark
    /// </summary>
    string Hash { get; }
}

/// <summary>
/// Options that can be set when adding a URL. Only need to set those
/// that are different from the defaults.
/// </summary>
public sealed class AddBookmarkOptions
{
    /// <summary>
    /// Title for the article. If not set, the service will resolve this
    /// from the destination URL of the bookmark being added
    /// </summary>
    public string Title = String.Empty;

    /// <summary>
    /// Description visible in the bookmarks list. Defaults to empty.
    /// </summary>
    public string Description = String.Empty;

    /// <summary>
    /// If the bookmark should be added directly to a folder other than the
    /// unread folder.
    /// </summary>
    public long DestinationFolderId = 0L;

    /// <summary>
    /// When enabled (the default), the destination URL will follow redirects.
    /// If you are confident that you are on the final URL, set to this to
    /// false. An example would be if you are sharing a page directly from
    /// a web browser
    /// </summary>
    public bool ResolveToFinalUrl = true;
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
    /// <param name="resultLimit">
    /// Limits the number of articles to return. Specify 0 for service default
    /// </param>
    /// <returns>
    /// List of bookmarks
    /// </returns>
    Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(string folderId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit);

    /// <summary>
    /// List the bookmarks for a specific folder. For wellknown folders
    /// <see cref="WellKnownFolderIds"/>. Other folders can be listed using
    /// the folder ID from the <see cref="FoldersClient"/> API
    /// </summary>
    /// <param name="folderId">Folder Id to list bookmarks for</param>
    /// <param name="haveInformation"><see cref="HaveStatus"/>'s for the
    /// known bookmarks &amp; state for the requested folder
    /// </param>
    /// <param name="resultLimit">
    /// Limits the number of articles to return. Specify 0 for service default
    /// </param>
    /// <returns>
    /// List of bookmarks
    /// </returns>
    Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(long folderId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit);

    /// <summary>
    /// Add a bookmark for the supplied URL.
    /// </summary>
    /// <param name="bookmarkUrl">URL to add</param>
    /// <param name="options">Options when adding this bookmark</param>
    /// <returns>The bookmark if successf</returns>
    Task<IInstapaperBookmark> AddAsync(Uri bookmarkUrl, AddBookmarkOptions? options);

    /// <summary>
    /// Updates the progress of a specific bookmark, explicitly.
    /// </summary>
    /// <param name="bookmark_id">Bookmark to update</param>
    /// <param name="progress">The progress, between 0.0 and 1.0</param>
    /// <param name="progress_timestamp">Time when progress was changed</param>
    /// <returns>The bookmark as returned by the service after the update</returns>
    Task<IInstapaperBookmark> UpdateReadProgressAsync(long bookmark_id, float progress, DateTime progress_timestamp);

    /// <summary>
    /// Sets the state of a bookmark to be 'Liked', irrespective of it's
    /// current like state
    /// </summary>
    /// <param name="bookmark_id">Bookmark to like</param>
    /// <returns>The updated bookmark</returns>
    Task<IInstapaperBookmark> LikeAsync(long bookmark_id);

    /// <summary>
    /// Sets the state of a bookmark to be 'unliked', irrespective of it's
    /// current like state
    /// </summary>
    /// <param name="id">Bookmark to unlike</param>
    /// <returns>The updated bookmark</returns>
    Task<IInstapaperBookmark> UnlikeAsync(long bookmark_id);

    /// <summary>
    /// Archives a bookmark -- which moves it out of the unread folder and
    /// into the well known 'Archive' folder
    /// </summary>
    /// <param name="id">Bookmark to archive</param>
    /// <returns>The updated bookmark</returns>
    Task<IInstapaperBookmark> ArchiveAsync(long bookmark_id);

    /// <summary>
    /// Unarchives a bookmark -- which moves it to the well known 'unread'
    /// folder, and is no longer in the 'Archive' folder
    /// </summary>
    /// <param name="id"></param>
    /// <returns>The updated bookmark</returns>
    Task<IInstapaperBookmark> UnarchiveAsync(long bookmark_id);

    /// <summary>
    /// Moves the supplied bookmark to the supplied folder.
    /// </summary>
    /// <param name="bookmark_id">Bookmark to move</param>
    /// <param name="folder_id">Folder to move the bookmark to</param>
    /// <returns>Bookmark after completing the move</returns>
    Task<IInstapaperBookmark> MoveAsync(long bookmark_id, long folder_id);

    /// <summary>
    /// Get the text of the bookmark from the service. This is returned in
    /// HTML format, intending to be displayed in a browser.
    ///
    /// Note, that if the bookmark is no longer available or cannot be
    /// parsed, an exception will be thrown.
    /// </summary>
    /// <param name="bookmark_id"></param>
    /// <returns></returns>
    Task<string> GetTextAsync(long bookmark_id);

    /// <summary>
    /// Delete a bookmark
    /// <param name="bookmark_id">Bookmark to delete</param>
    /// </summary>
    Task DeleteAsync(long bookmark_id);
}

public static class IBookmarksClientExtension
{
    /// <summary>
    /// List the bookmarks for a specific folder. For wellknown folders
    /// <see cref="WellKnownFolderIds"/>. Other folders can be listed using
    /// the folder ID from the <see cref="FoldersClient"/> API
    /// </summary>
    /// <param name="folderId">Wellknown Folder to list bookmarks for</param>
    /// <param name="limit">Number of articles to return. 0 specifies service default</param>
    /// <returns>
    /// List of bookmarks
    /// </returns>
    public static Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(this IBookmarksClient instance, string wellKnownFolderId, uint limit = 0)
    {
        return instance.ListAsync(wellKnownFolderId, null, limit);
    }

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
    public static Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(this IBookmarksClient instance, string wellKnownFolderId, IEnumerable<HaveStatus> haveInformation)
    {
        return instance.ListAsync(wellKnownFolderId, haveInformation, 0);
    }

    /// <summary>
    /// List the bookmarks for a specific folder. Other folders can be listed using
    /// the folder ID from the <see cref="FoldersClient"/> API
    /// </summary>
    /// <param name="folderId">Folder Id to list bookmarks for</param>
    /// <param name="limit">Number of articles to return. 0 specifies service default</param>
    /// <returns>
    /// List of bookmarks
    /// </returns>
    public static Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(this IBookmarksClient instance, long folder_id, uint limit = 0)
    {
        return instance.ListAsync(folder_id, null, limit);
    }

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
    public static Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(this IBookmarksClient instance, long folder_id, IEnumerable<HaveStatus> haveInformation)
    {
        return instance.ListAsync(folder_id, haveInformation, 0);
    }

    /// <summary>
    /// Add a bookmark for the supplied URL.
    /// </summary>
    /// <param name="bookmarkUrl">URL to add</param>
    /// <returns>The bookmark if successf</returns>
    public static Task<IInstapaperBookmark> AddAsync(this IBookmarksClient instance, Uri bookmarkUrl)
    {
        return instance.AddAsync(bookmarkUrl, null);
    }
}

internal sealed record Bookmark : IInstapaperBookmark
{
    internal static IInstapaperBookmark FromJsonElement(JsonElement bookmarkElement)
    {
        var id = bookmarkElement.GetProperty("bookmark_id").GetInt64();

        // Url
        var urlString = bookmarkElement.GetProperty("url").GetString();
        var url = new Uri(urlString);

        // Title & Description
        var title = bookmarkElement.GetProperty("title").GetString()!;
        var description = bookmarkElement.GetProperty("description").GetString()!;

        // Progress
        var progress = bookmarkElement.GetProperty("progress").GetSingle();
        var progressTimestampRaw = bookmarkElement.GetProperty("progress_timestamp").GetInt64();
        var progressTimestampUnixEpoch = DateTimeOffset.FromUnixTimeMilliseconds(progressTimestampRaw);
        var progressTimestamp = progressTimestampUnixEpoch.LocalDateTime;

        // Hash
        var hash = bookmarkElement.GetProperty("hash").GetString()!;

        // Liked status
        var likedRaw = bookmarkElement.GetProperty("starred").GetString()!;
        var liked = (likedRaw == "1");

        return new Bookmark()
        {
            Id = id,
            Url = url,
            Title = title,
            Description = description,
            Progress = progress,
            ProgressTimestamp = progressTimestamp,
            Liked = liked,
            Hash = hash
        };
    }

    public long Id { get; init; } = 0L;
    public Uri Url { get; init; } = new Uri("unset://unset");
    public string Title { get; init; } = String.Empty;
    public string Description { get; init; } = String.Empty;
    public float Progress { get; init; } = 0.0F;
    public DateTime ProgressTimestamp { get; init; } = DateTime.MinValue;
    public bool Liked { get; init; } = false;
    public string Hash { get; init; } = String.Empty;
}

/// <summary>
/// Lightweight information about the status of a bookmark for syncing
/// purposes. Encapsulates having the bookmark & it's state, as well as
/// progress & progress changed information
/// </summary>
public sealed record HaveStatus
{
    /// <summary>
    /// When you have all peices of the bookmark state, this constructor
    /// encapsulates that information.
    /// </summary>
    /// <param name="bookmark_id">Bookmark ID</param>
    /// <param name="hash">
    /// Hash of the last status (generated by the service, not locally)
    /// </param>
    /// <param name="readProgress">
    /// Amount of bookmark that has been read, between 0.0 and 1.0
    /// </param>
    /// <param name="changed">Last time the progress changed</param>
    public HaveStatus(long bookmark_id, string hash, float readProgress, DateTime changed) : this(bookmark_id, hash)
    {
        this.ReadProgress = readProgress;
        this.ProgressLastChanged = changed;
    }

    /// <summary>
    /// When you only have -- or only want to use -- the ID information
    /// </summary>
    /// <param name="id">Bookmark ID</param>
    public HaveStatus(long bookmark_id)
    {
        if (bookmark_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
        }

        this.Id = bookmark_id;
        this.Hash = String.Empty;
        this.ReadProgress = null;
        this.ProgressLastChanged = null;
    }

    /// <summary>
    /// With an ID & hash, you can use this constructor
    /// </summary>
    /// <param name="bookmark_id">Bookmark ID being represented</param>
    /// <param name="hash">Last known Service Hash for the bookmark state</param>
    public HaveStatus(long bookmark_id, string hash) : this(bookmark_id)
    {
        if (String.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentOutOfRangeException(nameof(hash), "Hash required for this bookmark");
        }

        this.Hash = hash;
    }

    /// <summary>
    /// ID of the Bookmark on the service
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Service-provided hash of the bookmark state. Can't be derived locally.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// The read progress of this bookmark as a percentage from 0.0 to 1.0
    /// </summary>
    public float? ReadProgress { get; }

    /// <summary>
    /// The unix epoch time that the progress was last updated.
    /// </summary>
    public DateTime? ProgressLastChanged { get; }

    /// <summary>
    /// The 'have' syntax is a very specific string, so overriding it here
    /// allows us to format it correctly. For more details on this format
    /// see <see href="https://www.instapaper.com/api">here</see>
    /// </summary>
    /// <returns>
    /// String representation of the have information to be passed to the
    /// service
    /// </returns>
    public override string ToString()
    {
        StringBuilder haveText = new StringBuilder(this.Id.ToString());

        if (!String.IsNullOrWhiteSpace(this.Hash))
        {
            haveText.AppendFormat(":{0}", this.Hash);
        }

        if (this.ReadProgress != null && this.ProgressLastChanged != null)
        {
            haveText.AppendFormat(":{0}:{1}", this.ReadProgress, new DateTimeOffset(this.ProgressLastChanged.Value).ToUnixTimeMilliseconds());
        }

        return haveText.ToString();
    }
}

/// <summary>
/// Bookmark operations for Instapaper -- adding removing, changing states
/// </summary>
public sealed class BookmarksClient : IBookmarksClient
{
    private readonly HttpClient client;

    public BookmarksClient(ClientInformation clientInformation)
    {
        this.client = OAuthMessageHandler.CreateOAuthHttpClient(clientInformation);
    }

    private Task<(IList<IInstapaperBookmark>, JsonElement? Meta)> PerformRequestAsync(Uri endpoint, string parameterName, string parameterValue)
    {
        var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>(parameterName, parameterValue) });
        return this.PerformRequestAsync(endpoint, content);
    }

    private Task<(IList<IInstapaperBookmark>, JsonElement? Meta)> PerformRequestAsync(Uri endpoint, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var content = new FormUrlEncodedContent(parameters);
        return this.PerformRequestAsync(endpoint, content);
    }

    private async Task<(IList<IInstapaperBookmark> Bookmarks, JsonElement? Meta)> PerformRequestAsync(Uri endpoint, HttpContent content)
    {
        // Request data convert to JSON
        var result = await this.client.PostAsync(endpoint, content);
        if (!result.IsSuccessStatusCode && Helpers.IsFatalStatusCode(result.StatusCode))
        {
            result.EnsureSuccessStatusCode();
        }

        var stream = await result.Content.ReadAsStreamAsync();
        var payload = JsonDocument.Parse(stream).RootElement;
        Debug.Assert(JsonValueKind.Array == payload.ValueKind, "API is always supposed to return an array as the root element");

        // Turn the JSON into strongly typed objects
        IList<IInstapaperBookmark> bookmarks = new List<IInstapaperBookmark>();
        JsonElement? meta = null;
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
                    // We don't process users
                    continue;

                case "meta":
                    Debug.Assert(meta == null, "Didn't expect more than one meta object");
                    meta = element;
                    continue;

                default:
                    Debug.Fail($"Unexpected return object type: {itemType}");
                    continue;
            }
        }

        return (bookmarks, meta);
    }

    /// <summary>
    /// Makes request, based only on the bookmark ID, and expects to only to
    /// return a single bookmark as the result.
    /// </summary>
    /// <param name="endpoint">URI to post the data to</param>
    /// <param name="bookmark_id">The bookmark to operate on</param>
    /// <returns>Single bookmark on success</returns>
    private async Task<IInstapaperBookmark> SingleBookmarkOperationAsync(Uri endpoint, long bookmark_id)
    {
        if (bookmark_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid bookmark");
        }

        var (result, _) = await this.PerformRequestAsync(endpoint, "bookmark_id", bookmark_id.ToString());

        Debug.Assert(result.Count == 1, $"Expected one bookmark in result, {result.Count} found");
        return result.First();
    }

    public async Task<(IList<IInstapaperBookmark>, IList<long>)> ListAsync(string wellKnownFolderId, IEnumerable<HaveStatus>? haveInformation, uint limit)
    {
        if (limit > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Article limit must be 500 or less");
        }

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

        if (limit != 0)
        {
            parameters.Add("limit", limit.ToString());
        }

        var (bookmarks, meta) = await this.PerformRequestAsync(EndPoints.Bookmarks.List, parameters);
        var deletedIds = new List<long>();

        // Parse the deleleted ID's if it's present
        if (meta != null && meta.Value.TryGetProperty("delete_ids", out var deletedIdsElement))
        {
            // Deleted IDs comes in as a comma separated string
            var rawDeletedIds = deletedIdsElement.GetString()!;
            var deletedIdsAsStrings = rawDeletedIds.Split(',');
            foreach (var stringId in deletedIdsAsStrings)
            {
                deletedIds.Add(Int64.Parse(stringId));
            }
        }

        return (bookmarks, deletedIds);
    }

    public Task<(IList<IInstapaperBookmark>, IList<long>)> ListAsync(long folder_id, IEnumerable<HaveStatus>? haveInformation, uint limit)
    {
        if (folder_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(folder_id), "Invalid Folder ID");
        }

        return this.ListAsync(folder_id.ToString(), haveInformation, limit);
    }

    public async Task<IInstapaperBookmark> AddAsync(Uri bookmarkUrl, AddBookmarkOptions? options)
    {
        if ((bookmarkUrl.Scheme != Uri.UriSchemeHttp)
            && (bookmarkUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only HTTP or HTTPS Urls are supported");
        }

        var parameters = new Dictionary<string, string>()
        {
            { "url", bookmarkUrl.ToString() }
        };

        if (options != null)
        {
            if (!String.IsNullOrWhiteSpace(options.Title))
            {
                parameters.Add("title", options.Title);
            }

            if (!String.IsNullOrWhiteSpace(options.Description))
            {
                parameters.Add("description", options.Description);
            }

            if (options.DestinationFolderId > 0L)
            {
                parameters.Add("folder_id", options.DestinationFolderId.ToString());
            }

            if (!options.ResolveToFinalUrl)
            {
                parameters.Add("resolve_final_url", "0");
            }
        }

        var (result, _) = await this.PerformRequestAsync(EndPoints.Bookmarks.Add, parameters);

        Debug.Assert(result.Count == 1, $"Expected one bookmark added, {result.Count} found");
        return result.First();
    }

    public async Task<IInstapaperBookmark> UpdateReadProgressAsync(long bookmark_id, float progress, DateTime progress_timestamp)
    {
        if (bookmark_id < 1L)
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

        var (result, _) = await this.PerformRequestAsync(EndPoints.Bookmarks.UpdateReadProgress, parameters);

        Debug.Assert(result.Count == 1, $"Expected one bookmark progress updated, {result.Count} found");
        return result.First();
    }

    public Task<IInstapaperBookmark> LikeAsync(long bookmark_id) => this.SingleBookmarkOperationAsync(EndPoints.Bookmarks.Star, bookmark_id);
    public Task<IInstapaperBookmark> UnlikeAsync(long bookmark_id) => this.SingleBookmarkOperationAsync(EndPoints.Bookmarks.Unstar, bookmark_id);
    public Task<IInstapaperBookmark> ArchiveAsync(long bookmark_id) => this.SingleBookmarkOperationAsync(EndPoints.Bookmarks.Archive, bookmark_id);
    public Task<IInstapaperBookmark> UnarchiveAsync(long bookmark_id) => this.SingleBookmarkOperationAsync(EndPoints.Bookmarks.Unarchive, bookmark_id);

    public async Task<IInstapaperBookmark> MoveAsync(long bookmark_id, long folder_id)
    {
        if (bookmark_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
        }

        if (folder_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(folder_id), "Invalid Folder ID");
        }

        var parameters = new Dictionary<string, string>()
        {
            { "bookmark_id", bookmark_id.ToString() },
            { "folder_id", folder_id.ToString() }
        };

        var (result, _) = await this.PerformRequestAsync(EndPoints.Bookmarks.Move, parameters);

        Debug.Assert(result.Count == 1, $"Expected one bookmark to be moved, {result.Count} found");
        return result.First();
    }

    public async Task<string> GetTextAsync(long bookmark_id)
    {
        if (bookmark_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>()
        {
            { "bookmark_id", bookmark_id.ToString() }
        });

        var result = await this.client.PostAsync(EndPoints.Bookmarks.GetText, content);
        if (!result.IsSuccessStatusCode)
        {
            if (Helpers.IsFatalStatusCode(result.StatusCode))
            {
                result.EnsureSuccessStatusCode();
            }

            // Assumption is that since it's 400, it'll contain an error
            // object in the standard format
            var stream = await result.Content.ReadAsStreamAsync();
            var payload = JsonDocument.Parse(stream).RootElement;
            Debug.Assert(JsonValueKind.Array == payload.ValueKind, "API is always supposed to return an array as the root element");

            foreach (var element in payload.EnumerateArray())
            {
                var itemType = element.GetProperty("type").GetString();
                if (itemType == "error")
                {
                    // Always throw an error when we encounter it, no matter
                    // if we've seen other (valid data)
                    throw ExceptionMapper.FromErrorJson(element);
                }
            }
        }

        return await result.Content.ReadAsStringAsync();
    }

    public async Task DeleteAsync(long bookmark_id)
    {
        if (bookmark_id < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(bookmark_id), "Invalid Bookmark ID");
        }

        _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Delete, "bookmark_id", bookmark_id.ToString());
    }
}
