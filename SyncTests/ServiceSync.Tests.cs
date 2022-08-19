using Codevoid.Instapaper;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;

using CurrentServiceStateFixture = Codevoid.Test.Instapaper.CurrentServiceStateFixture;
using Codevoid.Test.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.ServiceIntegration;

internal static class ComparisonExtensions
{
    internal static void AssertSameAs(this IEnumerable<DatabaseFolder> local, IEnumerable<IInstapaperFolder> remote)
    {
        var localList = local.OrderBy((f) => f.ServiceId!).ToList();
        var remoteList = remote.OrderBy((f) => f.Id).ToList();

        Assert.Equal(local.Count(), remote.Count());

        for (var index = 0; index < local.Count(); index += 1)
        {
            var localFolder = localList[index];
            var remoteFolder = remoteList[index];

            Assert.Equal(localFolder.ServiceId!, remoteFolder.Id);
            Assert.Equal(localFolder.Title, remoteFolder.Title);
            Assert.Equal(localFolder.Position, remoteFolder.Position);
            Assert.Equal(localFolder.ShouldSync, remoteFolder.SyncToMobile);
        }
    }

    internal static void AssertSameAs(this IEnumerable<DatabaseArticle> local, IEnumerable<IInstapaperBookmark> remote)
    {
        var localList = local.OrderBy((a) => a.Id).ToList();
        var remoteList = remote.OrderBy((a) => a.Id).ToList();

        Assert.Equal(local.Count(), remote.Count());

        for (var index = 0; index < local.Count(); index += 1)
        {
            var localArticle = localList[index];
            var remoteBookmark = remoteList[index];

            Assert.Equal(localArticle.Id, remoteBookmark.Id);
            Assert.Equal(localArticle.Url, remoteBookmark.Url);
            Assert.Equal(localArticle.Title, remoteBookmark.Title);
            Assert.Equal(localArticle.Description, remoteBookmark.Description);
            Assert.Equal(localArticle.ReadProgress, remoteBookmark.Progress);
            Assert.Equal(localArticle.ReadProgressTimestamp, remoteBookmark.ProgressTimestamp);
            Assert.Equal(localArticle.Liked, remoteBookmark.Liked);
            Assert.Equal(localArticle.Hash, remoteBookmark.Hash);

        }
    }
}

[CollectionDefinition(ServiceSyncTests.TEST_COLLECTION_NAME)]
public sealed class _SyncTestsCollection : ICollectionFixture<CurrentServiceStateFixture>
{ }

[Collection("ServiceSyncTests")]
public sealed class ServiceSyncTests : IAsyncLifetime
{
    #region Constants
    internal const string TEST_COLLECTION_NAME = "ServiceSyncTests";
    private const int MIN_FOLDERS = 2;
    private const int MIN_ARTICLES_PER_FOLDER = 2;
    #endregion

    private CurrentServiceStateFixture SharedState;
    private IEnumerable<IInstapaperFolder>? folders;

    public ServiceSyncTests(CurrentServiceStateFixture state)
    {
        this.SharedState = state;
    }

    public Task DisposeAsync()
    { return Task.CompletedTask; }

    public async Task InitializeAsync()
    {
        // This aspires to have no-folders, and all service-articles in the
        // unread folder. 

        // Re-initialize the service; this is intended to clean it up, but limit
        // the number of deletes and the number of adds we might do.
        await this.SharedState.InitializeAsync();

        // Initialize some folders
        var folders = (await this.SharedState.FoldersClient.ListAsync()).ToList();
        this.folders = folders;
        var foldersNeeded = Math.Max(0, MIN_FOLDERS - folders.Count());
        for (var index = 0; index < foldersNeeded; index += 1)
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var newFolder = await this.SharedState.FoldersClient.AddAsync(folderName);
            this.SharedState.UpdateOrSetRecentFolder(newFolder);
            folders.Add(newFolder);
        }

        // Initialize some articles
        var currentBookmarks = (await this.SharedState.BookmarksClient.ListAsync(WellKnownFolderIds.Unread)).Bookmarks.ToList();
        var bookmarksNeeded = Math.Max(0, ((folders.Count() + 2) * MIN_ARTICLES_PER_FOLDER) - currentBookmarks.Count());
        for (var index = 0; index < bookmarksNeeded; index += 1)
        {
            var newArticle = await this.SharedState.BookmarksClient.AddAsync(this.SharedState.GetNextAddableUrl());
            currentBookmarks.Add(newArticle);
            this.SharedState.UpdateOrSetRecentBookmark(newArticle);
        }

        // Sprinkle some articles across some folders, setting some states
        await this.SharedState.BookmarksClient.MoveAsync(currentBookmarks[0].Id, folders[0].Id);
        await this.SharedState.BookmarksClient.MoveAsync(currentBookmarks[1].Id, folders[0].Id);
        await this.SharedState.BookmarksClient.LikeAsync(currentBookmarks[0].Id);
        await this.SharedState.BookmarksClient.UpdateReadProgressAsync(currentBookmarks[1].Id, 0.5F, DateTime.Now);

        await this.SharedState.BookmarksClient.MoveAsync(currentBookmarks[2].Id, folders[1].Id);
        await this.SharedState.BookmarksClient.MoveAsync(currentBookmarks[3].Id, folders[1].Id);

        await this.SharedState.BookmarksClient.ArchiveAsync(currentBookmarks[4].Id);
        await this.SharedState.BookmarksClient.ArchiveAsync(currentBookmarks[5].Id);

        // Check we have enough in 'unread'
        var unreadBookmarks = await this.SharedState.BookmarksClient.ListAsync(WellKnownFolderIds.Unread);
        Assert.True(unreadBookmarks.Bookmarks.Count() > 1);
    }

    private async static Task AssertDatabaseAndRemoteMatch((IFolderDatabase Folders, IArticleDatabase Articles) database, (IFoldersClient Folders, IBookmarksClient Bookmarks) service)
    {
        // Check folders
        var remoteFoldersTask = service.Folders.ListAsync();
        var localFolders = database.Folders.ListAllCompleteUserFolders().ToList();
        var remoteFolders = await remoteFoldersTask;
        localFolders.AssertSameAs(remoteFolders);

        // Compare articles
        var remoteUnreadBookmarksTask = service.Bookmarks.ListAsync(WellKnownFolderIds.Unread);
        var remoteArchiveBookmarksTask = service.Bookmarks.ListAsync(WellKnownFolderIds.Archived);
        var remoteLikedBookmarksTask = service.Bookmarks.ListAsync(WellKnownFolderIds.Liked);

        // Compare unread articles
        var localUnreadArticles = database.Articles.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread);
        var remoteUnreadBookmarks = (await remoteUnreadBookmarksTask).Bookmarks;
        localUnreadArticles.AssertSameAs(remoteUnreadBookmarks);

        // Compare Archive
        var localArchiveArticles = database.Articles.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive);
        var remoteArchiveBookmarks = (await remoteArchiveBookmarksTask).Bookmarks;
        localArchiveArticles.AssertSameAs(remoteArchiveBookmarks);

        // Compare likes
        var localLikedArticles = database.Articles.ListLikedArticles();
        var remoteLikedArticles = (await remoteLikedBookmarksTask).Bookmarks;
        localLikedArticles.AssertSameAs(remoteLikedArticles);

        // Eagerly get the contents of the remote folders
        var remoteArticlesTasks = localFolders.Select((f) => service.Bookmarks.ListAsync(Convert.ToInt64(f.ServiceId!))).ToList();

        // Compare user folder contents
        for (var index = 0; index < localFolders.Count(); index += 1)
        {
            var localFolder = localFolders[index];
            var localArticles = database.Articles.ListArticlesForLocalFolder(localFolder.LocalId);
            var remoteArticles = (await remoteArticlesTasks[index]).Bookmarks;
            localArticles.AssertSameAs(remoteArticles);
        }
    }

    IDisposable GetLedger(IFolderDatabase folderDB, IArticleDatabase articleDB)
    {
        return InstapaperDatabase.GetLedger(folderDB, articleDB);
    }

    private async Task<(InstapaperSync SyncEngine, (SqliteConnection Connection,
                     IFolderDatabase FolderDB,
                     IFolderChangesDatabase FolderChangeDB,
                     IArticleDatabase ArticleDB,
                     IArticleChangesDatabase ArticleChangeDB) LocalDatabase)> SyncAndVerifyInitialRemoteState()
    {
        var localDatabase = TestUtilities.GetEmptyDatabase();
        var syncEngine = new InstapaperSync(localDatabase.FolderDB,
                                                          localDatabase.FolderChangeDB,
                                                          this.SharedState.FoldersClient,
                                                          localDatabase.ArticleDB,
                                                          localDatabase.ArticleChangeDB,
                                                          this.SharedState.BookmarksClient);

        await syncEngine.SyncEverythingAsync();

        await AssertDatabaseAndRemoteMatch((localDatabase.FolderDB, localDatabase.ArticleDB), (this.SharedState.FoldersClient, this.SharedState.BookmarksClient));

        return (syncEngine, localDatabase);
    }

    [Fact]
    public async Task RemoteStateCorrectlySyncsToEmptyLocalDatabase()
    {
        var state = await this.SyncAndVerifyInitialRemoteState();
        state.LocalDatabase.Connection.Close();
        state.LocalDatabase.Connection.Dispose();
    }

    [Fact]
    public async Task PendingAndRemoteUpdatesAreAppliedLocallyAndRemotely()
    {
        var (syncEngine, localDatabase) = await SyncAndVerifyInitialRemoteState();

        try
        {
            // List remote unread articles so we can fiddle with them
            var remoteUnreadBookmarksTask = this.SharedState.BookmarksClient.ListAsync(WellKnownFolderIds.Unread);

            // Get a local unread article, and some other folder
            var localUnreadArticle = localDatabase.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
            var firstUserFolder = localDatabase.FolderDB.FirstCompleteUserFolder();

            using (this.GetLedger(localDatabase.FolderDB, localDatabase.ArticleDB))
            {
                // Update it's progress
                localDatabase.ArticleDB.UpdateReadProgressForArticle(localUnreadArticle.ReadProgress + 0.1F, DateTime.Now, localUnreadArticle.Id);

                // Move it to a folder
                localDatabase.ArticleDB.MoveArticleToFolder(localUnreadArticle.Id, firstUserFolder.LocalId);
            }

            // Select the *other* unread article to mutate
            var remoteUnreadBookmarks = await remoteUnreadBookmarksTask;
            var remoteUnreadBookmark = remoteUnreadBookmarks.Bookmarks.First((b) => b.Id != localUnreadArticle.Id);

            // Mutate it
            remoteUnreadBookmark = await this.SharedState.BookmarksClient.UpdateReadProgressAsync(remoteUnreadBookmark.Id, remoteUnreadBookmark.Progress + 0.1F, DateTime.Now);

            // Move it to another folder
            await this.SharedState.BookmarksClient.MoveAsync(remoteUnreadBookmark.Id, firstUserFolder.ServiceId!.Value);

            await syncEngine.SyncEverythingAsync();

            await AssertDatabaseAndRemoteMatch((localDatabase.FolderDB, localDatabase.ArticleDB), (this.SharedState.FoldersClient, this.SharedState.BookmarksClient));
        }
        finally
        {
            localDatabase.Connection.Close();
            localDatabase.Connection.Dispose();
        }
        await Task.CompletedTask;
    }
}