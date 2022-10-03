using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Test.Instapaper;
using Codevoid.Utilities.OAuth;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public static class TestUtilities
{
    // Don't start IDs at 1, so we have space for 'older' articles to be added
    private static long nextArticleId = 100L;
    public static readonly Uri BASE_URI = new("https://www.codevoid.net");
    public const string SAMPLE_TITLE = "Codevoid";

    public static void ThrowIfValueIsAPIKeyHasntBeenSet(string valueToTest, string valueName)
    {
        if (!String.IsNullOrWhiteSpace(valueToTest) && (valueToTest != "PLACEHOLDER"))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(valueName, "You must replace the placeholder value. See README.md");
    }

    public static ClientInformation GetClientInformation()
    {
        ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.CONSUMER_KEY, nameof(InstapaperAPIKey.CONSUMER_KEY));
        ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.CONSUMER_KEY_SECRET, nameof(InstapaperAPIKey.CONSUMER_KEY_SECRET));
        ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.ACCESS_TOKEN, nameof(InstapaperAPIKey.ACCESS_TOKEN));
        ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.TOKEN_SECRET, nameof(InstapaperAPIKey.TOKEN_SECRET));

        var clientInfo = new ClientInformation(
            InstapaperAPIKey.CONSUMER_KEY,
            InstapaperAPIKey.CONSUMER_KEY_SECRET,
            InstapaperAPIKey.ACCESS_TOKEN,
            InstapaperAPIKey.TOKEN_SECRET
        );

        clientInfo.ProductName = "Codevoid+Instapaper+API+Tests";
        clientInfo.ProductVersion = "0.1";

        return clientInfo;
    }

    public static IDbConnection GetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateDatabaseIfNeeded();

        return connection;
    }

    public static ArticleRecordInformation GetRandomArticle()
    {
        return new(
            id: nextArticleId++,
            title: "Sample Article",
            url: new Uri($"/{nextArticleId}", UriKind.Relative),
            description: String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "ABC",
            liked: false
        );
    }

    // Don't start IDs at 1, so we have space for 'older' articles to be added
    private static long nextMockDatabaseArticleId = 100L;
    public static DatabaseArticle GetMockDatabaseArticle()
    {
        var id = nextMockDatabaseArticleId++;
        var reader = new MockReader(new (string Name, object? Data)[]
        {
            ("id", id),
            ("url", $"https://www.codevoid.net/{id}"),
            ("title", $"Sample {id}"),
            ("read_progress", 0.1F),
            ("read_progress_timestamp", DateTime.Now),
            ("hash", $"1234{id}"),
            ("liked", false),
            ("description", null)
        });

        return DatabaseArticle.FromRow(reader);
    }

    public static (DatabaseFolder Unread, DatabaseFolder Archive) GetMockDefaultFolders()
    {
        var unreadReader = new MockReader(new (string Name, object? Data)[]
        {
            ("local_id", 1L),
            ("service_id", -1L),
            ("title", "Home"),
            ("position", -100L),
            ("should_sync", 1L)
        });

        var archiveReader = new MockReader(new (string Name, object? Data)[]
        {
            ("local_id", 2L),
            ("service_id", -2L),
            ("title", "Archive"),
            ("position", -99L),
            ("should_sync", 1L)
        });

        return (DatabaseFolder.FromRow(unreadReader), DatabaseFolder.FromRow(archiveReader));
    }

    private static long nextMockDatabaseLocalFolderId = 100L;
    private static long nextMockDatabaseServiceFolderId = 1000L;
    private static long nextMockFolderPosition = 1L;
    public static DatabaseFolder GetMockSyncedDatabaseFolder()
    {
        var reader = new MockReader(new (string Name, object? Data)[]
        {
            ("local_id", nextMockDatabaseLocalFolderId++),
            ("service_id", nextMockDatabaseServiceFolderId++),
            ("title", "Archive"),
            ("position", nextMockFolderPosition++),
            ("should_sync", 1L)
        });

        return DatabaseFolder.FromRow(reader);
    }

    public static DatabaseFolder GetMockUnsyncedDatabaseFolder()
    {
        var reader = new MockReader(new (string Name, object? Data)[]
        {
            ("local_id", nextMockDatabaseLocalFolderId++),
            ("service_id", null),
            ("title", "Archive"),
            ("position", 0L),
            ("should_sync", 1L)
        });

        return DatabaseFolder.FromRow(reader);
    }

    // Because we're deleting things from databases, 'next' ID won't always be
    // 'highest plus one'; so instead of trying to account for that, just always
    // bump it up by one
    private static long nextServiceId = 200L;
    public static readonly Uri BASE_URL = new Uri("https://www.codevoid.net");

    private static long GetNextServiceId()
    {
        return Interlocked.Increment(ref nextServiceId);
    }

    public static (SqliteConnection Connection,
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

    public static DatabaseFolder AddCompleteFolderToDb(this IFolderDatabase instance)
    {
        var id = GetNextServiceId();
        return instance.AddKnownFolder(
            title: $"Sample Folder {id}",
            serviceId: id,
            position: id,
            shouldSync: true
        );
    }

    public static DatabaseArticle AddRandomArticleToDb(this IArticleDatabase instance, long folder = WellKnownLocalFolderIds.Unread)
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

    public static IEnumerable<DatabaseFolder> ListAllCompleteUserFolders(this IFolderDatabase instance)
    {
        var localFolders = from f in instance.ListAllUserFolders()
                           where f.ServiceId.HasValue && f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                           select f;

        return new List<DatabaseFolder>(localFolders);
    }

    public static DatabaseFolder FirstCompleteUserFolder(this IFolderDatabase instance)
    {
        return instance.ListAllCompleteUserFolders().First()!;
    }

    public static DatabaseArticle FirstArticleInFolder(this IArticleDatabase instance, long localFolderId)
    {
        return instance.ListArticlesForLocalFolder(localFolderId).First()!;
    }

    public static DatabaseArticle FirstUnlikedArticleInfolder(this IArticleDatabase instance, long localFolderId)
    {
        return instance.ListArticlesForLocalFolder(localFolderId).First((a) => !a.Liked)!;
    }

    public static void AssertFoldersListsAreSame(IFolderDatabase a, IFolderDatabase b)
    {
        var aFolders = a.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        var bFolders = b.ListAllCompleteUserFolders().OrderBy((f) => f.ServiceId);
        Assert.Equal(aFolders, bFolders, new CompareFoldersIgnoringLocalId());
    }

    public static void AssertNoPendingEdits(this IFolderChangesDatabase instance)
    {
        Assert.Empty(instance.ListPendingFolderAdds());
        Assert.Empty(instance.ListPendingFolderDeletes());
    }

    public static void AssertNoPendingEdits(this IArticleChangesDatabase instance)
    {
        Assert.Empty(instance.ListPendingArticleAdds());
        Assert.Empty(instance.ListPendingArticleDeletes());
        Assert.Empty(instance.ListPendingArticleMoves());
        Assert.Empty(instance.ListPendingArticleStateChanges());
    }

    public static Uri GetRandomUrl()
    {
        return new Uri(BASE_URL, GetNextServiceId().ToString());
    }
}