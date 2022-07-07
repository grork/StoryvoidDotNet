using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Instapaper;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

internal static class TestUtilities
{
    internal static (IDbConnection, IFolderDatabase, IFolderChangesDatabase, IDbConnection, FoldersClientOverDatabase) GetDatabases()
    {
        // Setup local database
        var localConnection = new SqliteConnection("Data Source=:memory:");
        localConnection.Open();
        InstapaperDatabase.CreateDatabaseIfNeeded(localConnection);
        var folderDb = InstapaperDatabase.GetFolderDatabase(localConnection);

        // Create a copy of that database, which will serve as the starting
        // point for the service database.
        var serviceConnection = new SqliteConnection("Data Source=:memory:");
        serviceConnection.Open();
        localConnection.BackupDatabase(serviceConnection);

        return (
            localConnection,
            folderDb,
            InstapaperDatabase.GetFolderChangesDatabase(localConnection),
            serviceConnection,
            new FoldersClientOverDatabase(InstapaperDatabase.GetFolderDatabase(serviceConnection))
        );
    }
}

internal class MockFolder : IInstapaperFolder
{
    public string Title { get; init; } = String.Empty;
    public bool SyncToMobile { get; init; } = true;
    public long Position { get; init; } = 0;
    public long Id { get; init; } = 0;
}

internal static class Extensions
{
    internal static IInstapaperFolder ToInstapaperFolder(this DatabaseFolder instance)
    {
        if (!instance.ServiceId.HasValue)
        {
            throw new ArgumentException("A service ID is required to convert a database folder to a service folder");
        }

        return new MockFolder()
        {
            Title = instance.Title,
            Id = instance.ServiceId!.Value,
            Position = instance.Position,
            SyncToMobile = instance.ShouldSync
        };
    }
}

internal class FoldersClientOverDatabase : IFoldersClient
{
    internal IFolderDatabase FolderDB { get; init; }

    internal FoldersClientOverDatabase(IFolderDatabase folderDb)
    {
        this.FolderDB = folderDb;
    }

    private long NextServiceId()
    {
        var max = (from f in this.FolderDB.ListAllFolders()
                   select f.ServiceId).Max();

        return (max!.Value + 1);
    }

    #region IFoldersClient Implementation
    public Task<IInstapaperFolder> AddAsync(string folderTitle)
    {
        var nextId = this.NextServiceId();
        var folder = new MockFolder()
        {
            Id = nextId,
            Position = nextId,
            Title = folderTitle,
            SyncToMobile = true
        };

        this.FolderDB.AddKnownFolder(
            title: folder.Title,
            serviceId: folder.Id,
            position: folder.Position,
            shouldSync: folder.SyncToMobile);

        return Task.FromResult<IInstapaperFolder>(folder);
    }

    public Task DeleteAsync(long folderId)
    {
        var folder = this.FolderDB.GetFolderByServiceId(folderId);
        if (folder is not null)
        {
            this.FolderDB.DeleteFolder(folder.LocalId);
        }
        return Task.CompletedTask;
    }

    public Task<IList<IInstapaperFolder>> ListAsync()
    {
        var localFolders = from f in this.FolderDB.ListAllFolders()
                           where f.ServiceId.HasValue && f.LocalId != WellKnownLocalFolderIds.Unread && f.LocalId != WellKnownLocalFolderIds.Archive
                           select f;

        return Task.FromResult<IList<IInstapaperFolder>>(new List<IInstapaperFolder>(localFolders.Select((f) => f.ToInstapaperFolder())));
    }
    #endregion
}