using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Codevoid.Test.Instapaper
{
    /// <summary>
    /// Fixture class to orchestrate initilization of service state. Lives through
    /// the full life of the test collection, and captures things such as (but not
    /// limited to):
    /// - Current Folders
    /// - Current Remote Bookmarks
    /// - Current Bookmark State
    /// - Added Bookmark count
    /// </summary>
    public class CurrentServiceStateFixture : IAsyncLifetime
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
            private async Task<IList<ulong>> ListFolderIds()
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

            private async Task DeleteFolder(ulong folderId)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "folder_id", folderId.ToString() }
                });

                _ = await this.PerformRequestAsync(EndPoints.Folders.Delete, content);
            }

            internal async Task DeleteAllFolders()
            {
                var folders = await this.ListFolderIds();
                LogMessage($"Found {folders.Count} folders");
                foreach (var id in folders)
                {
                    LogMessage($"Deleting folder {id}");
                    await this.DeleteFolder(id);
                }
            }
            #endregion

            #region Bookmarks State Reset
            private async Task<IList<(ulong id,
                                      bool liked,
                                      string hash,
                                      ulong progress_timestamp,
                                      string url)>> ListBookmarks(string wellKnownFolderId, string have = "")
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
                var payload = await this.PerformRequestAsync(EndPoints.Bookmarks.List, new FormUrlEncodedContent(contentKeys));
                LogMessage($"Listing Bookmarks for {wellKnownFolderId}: End");

                var bookmarks = new List<(ulong, bool, string, ulong, string)>();
                foreach (var element in payload.EnumerateArray())
                {
                    switch (element.GetProperty("type").GetString())
                    {
                        case "bookmark":
                            var id = element.GetProperty("bookmark_id").GetUInt64();
                            var liked = (element.GetProperty("starred").ToString()) == "1" ? true : false;
                            var hash = element.GetProperty("hash").GetString();
                            var progress_timestamp = element.GetProperty("progress_timestamp").GetUInt64();
                            var url = element.GetProperty("url").GetString();
                            bookmarks.Add((id, liked, hash, progress_timestamp, url));
                            break;

                        case "error":
                            throw new InvalidOperationException("Error listing bookmarks");

                        default:
                            continue;
                    }
                }

                LogMessage($"Found {bookmarks.Count} bookmarks");

                return bookmarks;
            }

            private async Task Unarchive(ulong bookmarkId)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "bookmark_id", bookmarkId.ToString() }
                });

                _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Unarchive, content);
            }

            private async Task Unlike(ulong bookmarkId)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "bookmark_id", bookmarkId.ToString() }
                });

                _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Unstar, content);
            }

            public async Task DeleteBookmark(ulong bookmarkId)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "bookmark_id", bookmarkId.ToString() }
                });

                _ = await this.PerformRequestAsync(EndPoints.Bookmarks.Delete, content);
            }

            internal async Task MoveArchivedBookmarksToUnread()
            {
                var archivedBookmarks = await this.ListBookmarks(WellKnownFolderIds.Archived);
                foreach (var bookmark in archivedBookmarks)
                {
                    await this.Unarchive(bookmark.id);
                }
            }

            internal async Task<IList<(ulong id, Uri uri)>> ResetAllUnreadItems()
            {
                // List bookmarks so we can compute hashes + known progress
                var unreadBookmarks = await this.ListBookmarks(WellKnownFolderIds.Unread);

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
                        await this.Unlike(bookmark.id);
                    }
                }

                // Actually reset them
                LogMessage("Performing reset with have information");
                _ = await this.ListBookmarks(WellKnownFolderIds.Unread, String.Join(',', haves));

                return uris;
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

        async Task IAsyncLifetime.DisposeAsync()
        {
            LogMessage("Starting Cleanup");
            await Task.CompletedTask;
            LogMessage("Completing Cleanup");
        }

        async Task IAsyncLifetime.InitializeAsync()
        {
            LogMessage("Starting Init");
            var apiHelper = new SimpleInstapaperApi(this.logger);

            LogMessage("Cleaning up Folders");
            await apiHelper.DeleteAllFolders();

            LogMessage("Cleaningup Bookmarks");
            await apiHelper.MoveArchivedBookmarksToUnread();
            var remoteBookmarks = await apiHelper.ResetAllUnreadItems();

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

            LogMessage($"There were {availableToAddUris} URIs available to add");

            if (availableToAddUris.Count < 1)
            {
                LogMessage("There weren't any URIs for adding, so deleting one");

                // There weren't any available URIs, so we need to select
                // one bookmark that is one of our test URLs and delete it
                var bookmarkToDelete = (from bookmark in remoteBookmarks
                                        where TestUrls.BasicRemoteTestUris.Contains(bookmark.uri)
                                        select bookmark).First();

                await apiHelper.DeleteBookmark(bookmarkToDelete.id);
                availableToAddUris.Add(bookmarkToDelete.uri);
            }

            this.availbleUris = availableToAddUris;
            LogMessage("Completing Init");
        }
        #endregion

        public CurrentServiceStateFixture(IMessageSink loggerInstance)
        {
            this.logger = loggerInstance;
            this.Folders = new List<IFolder>();
        }

        #region Folder API & State
        private IFoldersClient? _foldersClient;
        public IFoldersClient FoldersClient
        {
            get
            {
                if (this._foldersClient == null)
                {
                    this._foldersClient = new FoldersClient(TestUtilities.GetClientInformation());
                }

                return this._foldersClient;
            }
        }
        /// <summary>
        /// Current set of folders that we know about.
        /// </summary>
        public IList<IFolder> Folders { get; }

        /// <summary>
        /// Replace the folder list we have with a new one wholesale.
        /// </summary>
        /// <param name="folders">Folders that should replace existing set</param>
        internal void ReplaceFolderList(IEnumerable<IFolder> folders)
        {
            this.Folders.Clear();
            foreach (var folder in folders)
            {
                this.Folders.Add(folder);
            }
        }
        #endregion

        #region Bookmarks API & State
        public Uri NonExistantUrl => TestUrls.NonExistantPage;

        private IList<Uri> availbleUris = new List<Uri>();
        private IBookmarksClient? _bookmarksClient;
        public IBookmarksClient BookmarksClient
        {
            get
            {
                if (this._bookmarksClient == null)
                {
                    this._bookmarksClient = new BookmarksClient(TestUtilities.GetClientInformation());
                }

                return this._bookmarksClient;
            }
        }
        public IBookmark? RecentlyAddedBookmark { get; private set; }

        private IDictionary<string, IList<IBookmark>> bookmarksByFolder = new Dictionary<string, IList<IBookmark>>();
        public IList<IBookmark>? BookmarksForFolder(string forWellKnownFolder)
        {
            this.bookmarksByFolder.TryGetValue(forWellKnownFolder, out var bookmarks);
            return bookmarks;
        }

        public void UpdateBookmarksForFolder(IList<IBookmark> bookmarks, string forWellKnownFolder)
        {
            this.bookmarksByFolder[forWellKnownFolder] = bookmarks;
            this.RefreshAvailableBookmarksFromKnownBookmarks();
        }

        private void RefreshAvailableBookmarksFromKnownBookmarks()
        {
            var allBookmarkUrls = new HashSet<Uri>();
            // Map all remote URLs into URIs
            foreach (var kvp in this.bookmarksByFolder)
            {
                foreach (var b in kvp.Value)
                {
                    allBookmarkUrls.Add(b.Url);
                }
            }

            // Filter Test URIs by remote URIs
            var availableUris = (from uri in TestUrls.BasicRemoteTestUris
                                 where !allBookmarkUrls.Contains(uri)
                                 select uri).ToList();

            this.availbleUris = availableUris;
        }

        public Uri GetNextAddableUrl()
        {
            Assert.True(this.availbleUris.Count > 0); // Expected a URL to be available

            var uri = this.availbleUris.First();
            this.availbleUris.Remove(uri);

            return uri;
        }

        public void AddBookmark(IBookmark bookmark, string inFolder = WellKnownFolderIds.Unread)
        {
            var folderBookmarks = this.BookmarksForFolder(inFolder);
            if (folderBookmarks != null)
            {
                var existingBookmark = (from b in folderBookmarks
                                        where b.Id == bookmark.Id
                                        select b).FirstOrDefault();

                if (existingBookmark != null)
                {
                    // Need to remove it if it's there, so we can add it with updated
                    // information
                    folderBookmarks.Remove(existingBookmark);
                }

                folderBookmarks.Add(bookmark);
                this.RefreshAvailableBookmarksFromKnownBookmarks();
            }

            this.RecentlyAddedBookmark = bookmark;
        }
        #endregion
    }
}
