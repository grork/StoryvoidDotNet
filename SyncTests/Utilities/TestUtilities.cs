using System.Data;
using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace Codevoid.Test.Storyvoid;

internal static class TestUtilities
{
    // Because we're deleting things from databases, 'next' ID won't always be
    // 'highest plus one'; so instead of trying to account for that, just always
    // bump it up by one
    private static long nextServiceId = 100L;
    private static readonly Uri BASE_URL = new Uri("https://www.codevoid.net");

    private static long GetNextServiceId()
    {
        return Interlocked.Increment(ref nextServiceId);
    }

    internal static (SqliteConnection, IFolderDatabase, IFolderChangesDatabase, IArticleDatabase, IArticleChangesDatabase) GetEmptyDatabase()
    {
        // Setup local database
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InstapaperDatabase.CreateDatabaseIfNeeded(connection);

        var folderDb = InstapaperDatabase.GetFolderDatabase(connection);
        var folderChangesDb = InstapaperDatabase.GetFolderChangesDatabase(connection);
        var articleDb = InstapaperDatabase.GetArticleDatabase(connection);
        var articleChangesDb = InstapaperDatabase.GetArticleChangesDatabase(connection);

        return (connection, folderDb, folderChangesDb, articleDb, articleChangesDb);
    }

    internal static (SqliteConnection Connection, IFolderDatabase, IFolderChangesDatabase, IArticleDatabase, IArticleChangesDatabase) GetDatabases()
    {
        var (localConnection, folderDb, folderChangesDb, articleDb, articleChangesDb) = GetEmptyDatabase();
        PopulateDatabase(folderDb);

        return (
            localConnection,
            folderDb,
            folderChangesDb,
            articleDb,
            articleChangesDb
        );
    }

    internal static (SqliteConnection Connection, MockFolderService, MockBookmarksService) GetService()
    {
        // Create a copy of that database, which will serve as the starting
        // point for the service database.
        var (serviceConnection, serviceFolderDb, _, serviceArticleDb, _) = GetEmptyDatabase();
        return (
            serviceConnection,
            new MockFolderService(serviceFolderDb),
            new MockBookmarksService(serviceArticleDb, serviceFolderDb)
        );
    }

    private static void PopulateDatabase(IFolderDatabase folderDb)
    {
        foreach (var _ in Enumerable.Range(10, 20))
        {
            folderDb.AddCompleteFolderToDb();
        }
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

    internal static IList<DatabaseFolder> ListAllCompleteUserFolders(this IFolderDatabase instance)
    {
        var localFolders = from f in instance.ListAllFolders()
                           where f.ServiceId.HasValue && f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                           select f;

        return new List<DatabaseFolder>(localFolders);
    }

    internal static void AssertFoldersListsAreSame(IFolderDatabase a, IFolderDatabase b)
    {
        var aFolders = a.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        var bFolders = b.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        Assert.Equal(aFolders, bFolders, new CompareFoldersIgnoringLocalId());
    }

    internal static void AssertNoPendingAdds(this IFolderChangesDatabase instance)
    {
        Assert.Empty(instance.ListPendingFolderAdds());
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

        if((x is not null) && (y is not null))
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

    protected (
        IDbConnection Connection,
        IFolderDatabase FolderDB,
        IFolderChangesDatabase FolderChangesDB,
        IArticleDatabase ArticleDB,
        IArticleChangesDatabase ArticleChangesDB
    ) databases;

    protected (
        IDbConnection ServiceConnection,
        MockFolderService MockFolderService,
        MockBookmarksService MockBookmarksService
    ) service;

    protected Sync syncEngine;

    protected BaseSyncTest()
    {
        var databases = TestUtilities.GetDatabases();
        var service = TestUtilities.GetService();
        databases.Connection.BackupDatabase(service.Connection);

        this.databases = databases;
        this.service = service;

        this.SetSyncEngineFromDatabases();
    }

    [MemberNotNull(nameof(syncEngine))]
    protected void SetSyncEngineFromDatabases()
    {
        this.syncEngine = new Sync(
            this.databases.FolderDB,
            this.databases.FolderChangesDB,
            this.service.MockFolderService,
            this.databases.ArticleDB,
            this.databases.ArticleChangesDB,
            this.service.MockBookmarksService
        );
    }

    protected void SwitchToEmptyLocalDatabase()
    {
        this.DisposeLocalDatabase();
        this.databases = TestUtilities.GetEmptyDatabase();
        this.SetSyncEngineFromDatabases();

        // Make sure we have an empty database for this test.
        Assert.Equal(DEFAULT_FOLDER_COUNT, this.databases.FolderDB.ListAllFolders().Count);
    }

    protected void SwitchToEmptyServiceDatabase()
    {
        this.DisposeServiceDatabase();

        this.service = TestUtilities.GetService();
        Assert.Empty(service.MockFolderService.FolderDB.ListAllCompleteUserFolders());

        this.SetSyncEngineFromDatabases();
    }

    protected IDisposable GetLedger()
    {
        return InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
    }

    private void DisposeLocalDatabase()
    {
        this.databases.Connection.Close();
        this.databases.Connection.Dispose();
    }

    private void DisposeServiceDatabase()
    {
        this.service.ServiceConnection.Close();
        this.service.ServiceConnection.Dispose();
    }

    public void Dispose()
    {
        this.DisposeLocalDatabase();
        this.DisposeServiceDatabase();
    }
}