using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace Codevoid.Test.Storyvoid;

public static class TestUtilities
{
    // Because we're deleting things from databases, 'next' ID won't always be
    // 'highest plus one'; so instead of trying to account for that, just always
    // bump it up by one
    private static long nextServiceId = 100L;
    internal static readonly Uri BASE_URL = new Uri("https://www.codevoid.net");

    private static long GetNextServiceId()
    {
        return Interlocked.Increment(ref nextServiceId);
    }

    internal static (SqliteConnection Connection,
                     IFolderDatabase FolderDB,
                     IFolderChangesDatabase FolderChangeDB,
                     IArticleDatabase ArticleDB,
                     IArticleChangesDatabase ArticleChangeDB) GetEmptyDatabase()
    {
        // Setup local database
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateDatabaseIfNeeded();

        var folderDb = connection.GetFolderDatabase();
        var folderChangesDb = connection.GetFolderChangesDatabase();
        var articleDb = connection.GetArticleDatabase();
        var articleChangesDb = connection.GetArticleChangesDatabase();

        return (connection, folderDb, folderChangesDb, articleDb, articleChangesDb);
    }

    public static (SqliteConnection Connection,
                   IFolderDatabase FolderDB,
                   IFolderChangesDatabase FolderChangeDB,
                   IArticleDatabase ArticleDB,
                   IArticleChangesDatabase ArticleChangeDB) GetDatabases()
    {
        var (localConnection, folderDb, folderChangesDb, articleDb, articleChangesDb) = GetEmptyDatabase();
        PopulateDatabase(folderDb, articleDb);

        return (
            localConnection,
            folderDb,
            folderChangesDb,
            articleDb,
            articleChangesDb
        );
    }

    internal static (SqliteConnection Connection,
                     MockFolderService Folders,
                     MockBookmarksService Bookmarks) GetService()
    {
        // Create a copy of that database, which will serve as the starting
        // point for the service database.
        var (serviceConnection, serviceFolderDb, _, serviceArticleDb, _) = GetEmptyDatabase();
        return (
            serviceConnection,
            new MockFolderService(serviceFolderDb, serviceArticleDb),
            new MockBookmarksService(serviceArticleDb, serviceFolderDb)
        );
    }

    private static void PopulateDatabase(IFolderDatabase folderDb, IArticleDatabase articleDb)
    {
        // Create some random folders
        foreach (var _ in Enumerable.Range(10, 20))
        {
            var folder = folderDb.AddCompleteFolderToDb();

            // Put a random folder in it
            articleDb.AddRandomArticleToDb(folder.LocalId);
            var articleToLike = articleDb.AddRandomArticleToDb(folder.LocalId);
            articleDb.LikeArticle(articleToLike.Id);

        }

        // Add some articles to the unread folder
        articleDb.AddRandomArticleToDb();
        var articleWithProgress = articleDb.AddRandomArticleToDb();
        articleDb.UpdateReadProgressForArticle(0.2F, DateTime.Now, articleWithProgress.Id);

        // Add some articles to the archive folder
        articleDb.AddRandomArticleToDb(WellKnownLocalFolderIds.Archive);
        articleDb.AddRandomArticleToDb(WellKnownLocalFolderIds.Archive);

        // Add one more article to unread, but then *orphan it* to reproduce the
        // service side bug where when deleting a folder that contains articles
        // the articles effectively become 'orphaned'. The documentation says
        // they should be moved to the archive folder, but they aren't placed
        // anywhere (the bug). This introduces a scenario where liked articles
        // now *only* appear in the liked 'virtual' folder. Since this has been
        // happening for ~3 years, we should mimick the bug in our local testing
        var orphanedLikedArticle = articleDb.AddRandomArticleToDb();
        orphanedLikedArticle = articleDb.LikeArticle(orphanedLikedArticle.Id);
        articleDb.RemoveArticleFromAnyFolder(orphanedLikedArticle.Id);
    }

    internal static DatabaseFolder AddCompleteFolderToDb(this IFolderDatabase instance)
    {
        var id = GetNextServiceId();
        return instance.AddKnownFolder(
            title: $"Sample Folder {id}",
            serviceId: id,
            position: id,
            shouldSync: true
        );
    }

    internal static DatabaseArticle AddRandomArticleToDb(this IArticleDatabase instance, long folder = WellKnownLocalFolderIds.Unread)
    {
        var id = GetNextServiceId();
        return instance.AddArticleToFolder(new(
            id: id,
            title: $"Title {id}",
            url: GetRandomUrl(),
            description: String.Empty,
            readProgress: 0.0f,
            readProgressTimestamp: DateTime.Now,
            hash: "1234",
            liked: false
        ), folder);
    }

    internal static IEnumerable<DatabaseFolder> ListAllCompleteUserFolders(this IFolderDatabase instance)
    {
        var localFolders = from f in instance.ListAllUserFolders()
                           where f.ServiceId.HasValue && f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                           select f;

        return new List<DatabaseFolder>(localFolders);
    }

    internal static DatabaseFolder FirstCompleteUserFolder(this IFolderDatabase instance)
    {
        return instance.ListAllCompleteUserFolders().First()!;
    }

    internal static DatabaseArticle FirstArticleInFolder(this IArticleDatabase instance, long localFolderId)
    {
        return instance.ListArticlesForLocalFolder(localFolderId).First()!;
    }

    internal static DatabaseArticle FirstUnlikedArticleInfolder(this IArticleDatabase instance, long localFolderId)
    {
        return instance.ListArticlesForLocalFolder(localFolderId).First((a) => !a.Liked)!;
    }

    internal static void AssertFoldersListsAreSame(IFolderDatabase a, IFolderDatabase b)
    {
        var aFolders = a.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        var bFolders = b.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        Assert.Equal(aFolders, bFolders, new CompareFoldersIgnoringLocalId());
    }

    internal static void AssertNoPendingEdits(this IFolderChangesDatabase instance)
    {
        Assert.Empty(instance.ListPendingFolderAdds());
        Assert.Empty(instance.ListPendingFolderDeletes());
    }

    internal static void AssertNoPendingEdits(this IArticleChangesDatabase instance)
    {
        Assert.Empty(instance.ListPendingArticleAdds());
        Assert.Empty(instance.ListPendingArticleDeletes());
        Assert.Empty(instance.ListPendingArticleMoves());
        Assert.Empty(instance.ListPendingArticleStateChanges());
    }

    internal static Uri GetRandomUrl()
    {
        return new Uri(BASE_URL, GetNextServiceId().ToString());
    }
}

internal class CompareFoldersIgnoringLocalId : IEqualityComparer<DatabaseFolder>
{
    public bool Equals(DatabaseFolder? x, DatabaseFolder? y)
    {
        if (x == y)
        {
            return true;
        }

        if ((x is not null) && (y is not null))
        {
            return (x.Title == y.Title)
                && (x.ServiceId == y.ServiceId)
                && (x.Position == y.Position)
                && (x.ShouldSync == y.ShouldSync);
        }

        return false;
    }

    public int GetHashCode([DisallowNull] DatabaseFolder obj)
    {
        return obj.GetHashCode();
    }
}

public abstract class BaseSyncTest : IDisposable
{
    protected const int DEFAULT_FOLDER_COUNT = 2;
    private IDbConnection connection;
    private IDbConnection serviceConnection;

    protected (
        IFolderDatabase FolderDB,
        IFolderChangesDatabase FolderChangesDB,
        IArticleDatabase ArticleDB,
        IArticleChangesDatabase ArticleChangesDB
    ) databases;

    protected (
        MockFolderService FoldersClient,
        MockBookmarksService BookmarksClient
    ) service;

    protected InstapaperSync syncEngine;

    protected BaseSyncTest()
    {
        var databases = TestUtilities.GetDatabases();
        var service = TestUtilities.GetService();
        databases.Connection.BackupDatabase(service.Connection);

        this.connection = databases.Connection;
        this.databases = (databases.FolderDB, databases.FolderChangeDB, databases.ArticleDB, databases.ArticleChangeDB);

        this.serviceConnection = service.Connection;
        this.service = (service.Folders, service.Bookmarks);

        this.SetSyncEngineFromDatabases();
    }

    [MemberNotNull(nameof(syncEngine))]
    internal void SetSyncEngineFromDatabases(IDatabaseSyncEventSource? clearingHouse = null)
    {
        this.syncEngine = new InstapaperSync(
            this.databases.FolderDB,
            this.databases.FolderChangesDB,
            this.service.FoldersClient,
            this.databases.ArticleDB,
            this.databases.ArticleChangesDB,
            this.service.BookmarksClient,
            clearingHouse
        );
    }

    protected void SwitchToEmptyLocalDatabase()
    {
        this.DisposeLocalDatabase();
        var databases = TestUtilities.GetEmptyDatabase();
        this.connection = databases.Connection;
        this.databases = (databases.FolderDB, databases.FolderChangeDB, databases.ArticleDB, databases.ArticleChangeDB);
        this.SetSyncEngineFromDatabases();

        // Make sure we have an empty database for this test.
        Assert.Equal(DEFAULT_FOLDER_COUNT, this.databases.FolderDB.ListAllFolders().Count());
    }

    protected void SwitchToEmptyServiceDatabase()
    {
        this.DisposeServiceDatabase();

        var service = TestUtilities.GetService();
        this.serviceConnection = service.Connection;
        this.service = (service.Folders, service.Bookmarks);
        Assert.Empty(service.Folders.FolderDB.ListAllCompleteUserFolders());

        this.SetSyncEngineFromDatabases();
    }

    protected IDbTransaction StartTransactionForLocalDatabase()
    {
        return this.connection.BeginTransaction();
    }

    protected IDisposable GetLedger()
    {
        return InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
    }

    private void DisposeLocalDatabase()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    private void DisposeServiceDatabase()
    {
        this.serviceConnection.Close();
        this.serviceConnection.Dispose();
    }

    public void Dispose()
    {
        this.DisposeLocalDatabase();
        this.DisposeServiceDatabase();
    }
}