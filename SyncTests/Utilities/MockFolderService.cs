using Codevoid.Instapaper;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

internal static class MockExtensions
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

internal class MockFolder : IInstapaperFolder
{
    public string Title { get; init; } = String.Empty;
    public bool SyncToMobile { get; init; } = true;
    public long Position { get; init; } = 0;
    public long Id { get; init; } = 0;
}

internal class MockFolderService : IFoldersClient
{
    internal IFolderDatabase FolderDB { get; init; }

    internal MockFolderService(IFolderDatabase folderDb)
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
        var localFolders = this.FolderDB.ListAllCompleteUserFolders();

        return Task.FromResult<IList<IInstapaperFolder>>(new List<IInstapaperFolder>(localFolders.Select((f) => f.ToInstapaperFolder())));
    }
    #endregion
}