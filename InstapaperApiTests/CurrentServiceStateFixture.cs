using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Codevoid.Test.Instapaper;

// See https://github.com/dotnet/runtime/issues/38494
// tl;dr: The nullability of `FormUrlEncodedContent` changed to allow nulls,
//        which with improved compilers makes it hard to call. The
//        recommended solution is a cast. It's long, so using a type alias
//        to make it easy to fix later.
using NullableStringEnumerable = IEnumerable<KeyValuePair<string?, string?>>;

/// <summary>
/// Fixture class to orchestrate initilization of service state. Lives through
/// the full life of the test collection, and captures things such as (but not
/// limited to):
/// - Current Folders
/// - Current Remote Bookmarks
/// - Current Bookmark State
/// - Added Bookmark count
/// </summary>
public sealed class CurrentServiceStateFixture : IAsyncLifetime
{
    /// <summary>
    /// A thin, simple, wrapper on the Instapaper Service API to facilitate
    /// cleaning up the state. It is intentionally separate from the actual
    /// API so we aren't trying to clean up with an API that is also being
    /// changed leading to poor test state.
    /// </summary>
    private class SimpleInstapaperApi
    {
        private IMessageSink logger;
        private readonly HttpClient httpClient = OAuthMessageHandler.CreateOAuthHttpClient(TestUtilities.GetClientInformation());

        internal SimpleInstapaperApi(IMessageSink logger) => this.logger = logger;
        private void LogMessage(string message) => this.logger.OnMessage(new DiagnosticMessage(message));

        private async Task<JsonElement> PerformRequestAsync(Uri endpoint, HttpContent content)
        {
            LogMessage($"Requesting {endpoint.ToString()}");

            // Request data convert to JSON
            var result = await this.httpClient.PostAsync(endpoint, content);
            result.EnsureSuccessStatusCode();

            var stream = await result.Content.ReadAsStreamAsync();
            var payload = JsonDocument.Parse(stream).RootElement;
            Debug.Assert(JsonValueKind.Array == payload.ValueKind, "API is always supposed to return an array as the root element");

            return payload;
        }

        #region Folder State Reset
        private async Task<IEnumerable<ulong>> ListFolderIdsAsync()
        {
            LogMessage("Requesting folders: Start");
            var payload = await this.PerformRequestAsync(EndPoints.Folders.List, new StringContent(String.Empty));
            LogMessage("Requesting folders: End");

            List<ulong> folders = new List<ulong>();
            foreach (var element in payload.EnumerateArray())
            {
                switch (element.GetProperty("type").GetString())
                {
                    case "folder":
                        var folderId = element.GetProperty("folder_id").GetUInt64();
                        folders.Add(folderId);
                        break;

                    case "error":
                        throw new InvalidOperationException("Error listing folders");

                    default:
                        continue;
                }
            }

            return folders;
        }

        private async Task DeleteFolderAsync(ulong folderId)
        {
            var content = new FormUrlEncodedContent((NullableStringEnumerable)new Dictionary<string, string>()
            {
                { "folder_id", folderId.ToString() }
            });

            _ = await this.PerformRequestAsync(EndPoints.Folders.Delete, content);
        }

        internal async Task DeleteAllFoldersAsync()
        {
            var folders = await this.ListFolderIdsAsync();
            LogMessage($"Found {folders.Count()} folders");
            foreach (var id in folders)
            {
                // Since Instapaper has a bug today that instead of following
                // the API documentation of 'a deleted folder moves its contents
                // to the archive folder', we'll move things to archive
                // ourselves. We can't move to 'unread' directly because that
                // costs an 'add'.
                LogMessage($"Listing articles in folder {id}");
                var bookmarks = await this.ListBookmarksAsync(id.ToString());
                foreach (var bookmark in bookmarks)
                {
                    await this.ArchiveAsync(bookmark.id);
                }

                LogMessage($"Deleting folder {id}");
                await this.DeleteFolderAsync(id);
            }
        }
        #endregion

        #region Bookmarks State Reset
        private async Task<IEnumerable<(ulong id,
                                  bool liked,
                                  string hash,
                                  ulong progress_timestamp,
                                  string url)>> ListBookmarksAsync(string wellKnownFolderId, string have = "")
        {
            var contentKeys = new Dictionary<string, string>()
            {
                { "folder_id", wellKnownFolderId }
            };

            if (!String.IsNullOrWhiteSpace(have))
            {
                contentKeys.Add("have", have);
            }

            LogMessage($"Listing Bookmarks for {wellKnownFolderId}: Start");
            var payload = await this.PerformRequestAsync(EndPoints.Bookmarks.List, new FormUrlEncodedContent((NullableStringEnumerable)contentKeys));
            LogMessage($"Listing Bookmarks for {wellKnownFolderId}: End");

            var bookmarks = new List<(ulong, bool, string, ulong, string)>();
            foreach (var element in payload.EnumerateArray())
            {
                switch (element.GetProperty("type").GetString())
                {
                    case "bookmark":
                        var id = element.GetProperty("bookmark_id").GetUInt64();
                        var liked = (element.GetProperty("starred").ToString()) == "1" ? true : false;
                        var hash = element.GetProperty("hash").GetString()!;
                        var progress_timestamp = element.GetProperty("progress_timestamp").GetUInt64();
                        var url = element.GetProperty("url").GetString()!;
                        bookmarks.Add((id, liked, hash, progress_timestamp, url));
                        break;

                    case "error":
                        throw new InvalidOperationException("Error listing bookmarks");

                    default:
                        continue;
                }
            }

            LogMessage($"Found {bookmarks.Count()} bookmarks");

            return bookmarks;
        }

        private async Task ArchiveAsync(ulong bookmarkId)
        {
            var content = new FormUrlEncodedContent((NullableStringEnumerable)new Dictionary<string, string>()
            {
                { "bookmark_id", bookmarkId.ToString() }
            });

            _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Star, content);
        }

        private async Task UnarchiveAsync(ulong bookmarkId)
        {
            var content = new FormUrlEncodedContent((NullableStringEnumerable)new Dictionary<string, string>()
            {
                { "bookmark_id", bookmarkId.ToString() }
            });

            _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Unarchive, content);
        }

        private async Task UnlikeAsync(ulong bookmarkId)
        {
            var content = new FormUrlEncodedContent((NullableStringEnumerable)new Dictionary<string, string>()
            {
                { "bookmark_id", bookmarkId.ToString() }
            });

            _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Unstar, content);
        }

        public async Task DeleteBookmarkAsync(ulong bookmarkId)
        {
            var content = new FormUrlEncodedContent((NullableStringEnumerable)new Dictionary<string, string>()
            {
                { "bookmark_id", bookmarkId.ToString() }
            });

            _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Delete, content);
        }

        internal async Task MoveArchivedBookmarksToUnreadAsync()
        {
            var archivedBookmarks = await this.ListBookmarksAsync(WellKnownFolderIds.Archived);
            foreach (var bookmark in archivedBookmarks)
            {
                await this.UnarchiveAsync(bookmark.id);
            }
        }

        internal async Task<IEnumerable<(ulong id, Uri uri)>> ResetAllUnreadItemsAsync()
        {
            // List bookmarks so we can compute hashes + known progress
            var unreadBookmarks = await this.ListBookmarksAsync(WellKnownFolderIds.Unread);

            // Create Have Values for all the bookmarks to reset their progress
            // to zero. Using the have capability allows this to be one
            // request, rather than multiple -- e.g. faster!
            // We're still going to burn a request for each one to unlike it
            var haves = new List<string>();
            var uris = new List<(ulong, Uri)>();
            foreach (var bookmark in unreadBookmarks)
            {
                var haveValue = $"{bookmark.id}:{bookmark.hash}:0.0:{bookmark.progress_timestamp + 1}";
                haves.Add(haveValue);

                uris.Add((bookmark.id, new Uri(bookmark.url)));

                // Reset the like status
                if (bookmark.liked)
                {
                    await this.UnlikeAsync(bookmark.id);
                }
            }

            // Actually reset them
            LogMessage("Performing reset with have information");
            _ = await this.ListBookmarksAsync(WellKnownFolderIds.Unread, String.Join(',', haves));

            return uris;
        }

        internal async Task UnlikeAllLikedArticlesAsync()
        {
            var likedArticles = await this.ListBookmarksAsync(WellKnownFolderIds.Liked);
            foreach (var article in likedArticles)
            {
                LogMessage($"Unliking {article.id}");
                await this.UnlikeAsync(article.id);
            }
        }
        #endregion
    }

    /// <summary>
    /// Well known URLs that we control, and are used for testing purposes
    /// </summary>
    private static class TestUrls
    {
        private readonly static Uri BaseTestUri = new Uri("https://www.codevoid.net/articlevoidtest/");
        internal readonly static Uri TestPage1 = new Uri(BaseTestUri, "_TestPage1.html");
        internal readonly static Uri TestPage2 = new Uri(BaseTestUri, "_TestPage2.html");
        internal readonly static Uri TestPage3 = new Uri(BaseTestUri, "_TestPage3.html");
        internal readonly static Uri TestPage4 = new Uri(BaseTestUri, "_TestPage4.html");
        internal readonly static Uri TestPage5 = new Uri(BaseTestUri, "_TestPage5.html");
        internal readonly static Uri TestPage6 = new Uri(BaseTestUri, "_TestPage6.html");
        internal readonly static Uri TestPage7 = new Uri(BaseTestUri, "_TestPage7.html");
        internal readonly static Uri TestPage8 = new Uri(BaseTestUri, "_TestPage8.html");
        internal readonly static Uri TestPage9 = new Uri(BaseTestUri, "_TestPage9.html");
        internal readonly static Uri TestPage10 = new Uri(BaseTestUri, "_TestPage10.html");
        internal readonly static Uri TestPage11 = new Uri(BaseTestUri, "_TestPage11.html");
        internal readonly static Uri TestPage12 = new Uri(BaseTestUri, "_TestPage12.html");
        internal readonly static Uri TestPage13 = new Uri(BaseTestUri, "_TestPage13.html");
        internal readonly static Uri TestPage14 = new Uri(BaseTestUri, "_TestPage14.html");
        internal readonly static Uri TestPage15 = new Uri(BaseTestUri, "_TestPage15.html");
        internal readonly static Uri TestPage16 = new Uri(BaseTestUri, "_TestPage16.html");
        internal readonly static Uri TestPageWithImages = new Uri(BaseTestUri, "TestPageWithImage.html");

        // This page should always 404
        internal readonly static Uri NonExistantPage = new Uri(BaseTestUri, "NotThere.html");

        // Basic Test URIs
        internal static IReadOnlyCollection<Uri> BasicRemoteTestUris =>
            ImmutableList.Create(TestPage1,
                                 TestPage2,
                                 TestPage3,
                                 TestPage4,
                                 TestPage5,
                                 TestPage6,
                                 TestPage7,
                                 TestPage8);
    }

    #region IAsyncLifetime
    private IMessageSink logger;
    private void LogMessage(string message) => this.logger.OnMessage(new DiagnosticMessage(message));

    public async Task DisposeAsync()
    {
        LogMessage("Starting Cleanup");

        (this.FoldersClient as IDisposable)?.Dispose();
        (this.BookmarksClient as IDisposable)?.Dispose();

        await Task.CompletedTask;
        LogMessage("Completing Cleanup");
    }

    public async Task InitializeAsync()
    {
        LogMessage("Starting Init");
        var apiHelper = new SimpleInstapaperApi(this.logger);

        LogMessage("Cleaning up Folders");
        await apiHelper.DeleteAllFoldersAsync();

        LogMessage("Cleaning up liked Articles");
        await apiHelper.UnlikeAllLikedArticlesAsync();

        LogMessage("Cleaning up Bookmarks");
        await apiHelper.MoveArchivedBookmarksToUnreadAsync();
        var remoteBookmarks = (await apiHelper.ResetAllUnreadItemsAsync()).ToList();

        // Collect all the remote test bookmarks that are still available
        // to be added e.g. those that *don't* appear in the remote list,
        // and are part of our known set of URLs
        var availableToAddUris = new List<Uri>(TestUrls.BasicRemoteTestUris);
        foreach (var remoteBookmark in remoteBookmarks)
        {
            if (availableToAddUris.Contains(remoteBookmark.uri))
            {
                availableToAddUris.Remove(remoteBookmark.uri);
            }
        }

        LogMessage($"There were {availableToAddUris.Count()} URIs available to add");

        if (availableToAddUris.Count() < 1)
        {
            LogMessage("There weren't any URIs for adding, so deleting one");

            // There weren't any available URIs, so we need to select
            // one bookmark that is one of our test URLs and delete it
            var bookmarkToDelete = (from bookmark in remoteBookmarks
                                    where TestUrls.BasicRemoteTestUris.Contains(bookmark.uri)
                                    select bookmark).First();

            await apiHelper.DeleteBookmarkAsync(bookmarkToDelete.id);
            remoteBookmarks.Remove(bookmarkToDelete);
            availableToAddUris.Add(bookmarkToDelete.uri);
        }

        this.available = availableToAddUris;
        this.RemoteBookmarksAtStart = remoteBookmarks;
        LogMessage("Completing Init");
    }
    #endregion

    public CurrentServiceStateFixture(IMessageSink loggerInstance)
    {
        this.logger = loggerInstance;
    }

    #region Folder API & State
    private Lazy<IFoldersClient> _foldersClient = new Lazy<IFoldersClient>(
        () => new FoldersClient(TestUtilities.GetClientInformation()));

    public IFoldersClient FoldersClient => this._foldersClient.Value;

    public IInstapaperFolder? RecentlyAddedFolder { get; private set; }
    public void UpdateOrSetRecentFolder(IInstapaperFolder? folder)
    {
        this.RecentlyAddedFolder = folder;
    }
    #endregion

    #region Bookmarks API & State
    public Uri NonExistantUrl => TestUrls.NonExistantPage;

    private IList<Uri> available = new List<Uri>();
    private Lazy<IBookmarksClient> _bookmarksClient =
        new Lazy<IBookmarksClient>(() => new BookmarksClient(TestUtilities.GetClientInformation()));
    public IBookmarksClient BookmarksClient => this._bookmarksClient.Value;

    public IInstapaperBookmark? RecentlyAddedBookmark { get; private set; }

    public void UpdateOrSetRecentBookmark(IInstapaperBookmark? bookmark)
    {
        this.RecentlyAddedBookmark = bookmark;
    }

    public IInstapaperBookmark? NotFoundBookmark { get; private set; }

    internal void SetNotFoundBookmark(IInstapaperBookmark bookmark)
    {
        this.NotFoundBookmark = bookmark;
    }

    public Uri GetNextAddableUrl()
    {
        Assert.True(this.available.Count() > 0); // Expected a URL to be available

        var uri = this.available.First();
        this.available.Remove(uri);

        return uri;
    }

    public IEnumerable<(ulong id, Uri uri)> RemoteBookmarksAtStart { get; private set; } = new List<(ulong, Uri)>();
    #endregion
}
