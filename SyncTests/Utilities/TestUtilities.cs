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

    private static long GetNextServiceId()
    {
        return Interlocked.Increment(ref nextServiceId);
    }

    internal static (SqliteConnection, IFolderDatabase, IFolderChangesDatabase, IArticleDatabase) GetEmptyDatabase()
    {
        // Setup local database
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InstapaperDatabase.CreateDatabaseIfNeeded(connection);

        var folderDb = InstapaperDatabase.GetFolderDatabase(connection);
        var folderChangesDb = InstapaperDatabase.GetFolderChangesDatabase(connection);
        var articleDb = InstapaperDatabase.GetArticleDatabase(connection);

        return (connection, folderDb, folderChangesDb, articleDb);
    }

    internal static (IDbConnection, IFolderDatabase, IFolderChangesDatabase, IArticleDatabase, IDbConnection, MockFolderService) GetDatabases()
    {
        var (localConnection, folderDb, folderChangesDb, articleDb) = GetEmptyDatabase();
        PopulateDatabase(folderDb);

        // Create a copy of that database, which will serve as the starting
        // point for the service database.
        var (serviceConnection, serviceFolderDb, _, _) = GetEmptyDatabase();
        localConnection.BackupDatabase(serviceConnection);

        return (
            localConnection,
            folderDb,
            folderChangesDb,
            articleDb,
            serviceConnection,
            new MockFolderService(serviceFolderDb)
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