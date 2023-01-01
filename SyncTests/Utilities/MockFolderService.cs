using System.Data;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

internal static class MockFolderExtensions
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

public class MockFolderService : IFoldersClient
{
    internal IFolderDatabase FolderDB { get; init; }
    private IArticleDatabase ArticleDB { get; init; }

    /// <summary>
    /// When performing async operations (e.g. most of the interface
    /// implementation), actually delay them so we get the full async flow.
    /// </summary>
    public bool DelayAsyncOperations = false;

    internal MockFolderService(IFolderDatabase folderDb, IArticleDatabase articleDb)
    {
        this.FolderDB = folderDb;
        this.ArticleDB = articleDb;
    }

    private long NextServiceId()
    {
        var max = (from f in this.FolderDB.ListAllFolders()
                   select f.ServiceId).Max();

        return (max!.Value + 1);
    }

    /// <summary>
    /// Returns a task with a delay if the <see cref="DelayAsyncOperations"/>
    /// is true. Otherwise just returns an already completed task.
    /// </summary>
    private Task MaybeDelay()
    {
        if (!this.DelayAsyncOperations)
        {
            return Task.CompletedTask;
        }

        return Task.Delay(1);
    }

    #region IFoldersClient Implementation
    public async Task<IInstapaperFolder> AddAsync(string folderTitle)
    {
        await this.MaybeDelay();

        var nextId = this.NextServiceId();
        var folder = new MockFolder()
        {
            Id = nextId,
            Position = nextId,
            Title = folderTitle,
            SyncToMobile = true
        };

        try
        {
            this.FolderDB.AddKnownFolder(
                title: folder.Title,
                serviceId: folder.Id,
                position: folder.Position,
                shouldSync: folder.SyncToMobile);
        }
        catch (DuplicateNameException)
        {
            // Map the database exception in to the same as a service exception
            throw new DuplicateFolderException();
        }

        return folder;
    }

    public async Task DeleteAsync(long folderId)
    {
        await this.MaybeDelay();
        var folder = this.FolderDB.GetFolderByServiceId(folderId);
        if (folder is null)
        {
            // Folder was already missing, so throw appropriate exception
            throw new EntityNotFoundException();
        }

        // We need to deleted contained articles first, to mimic what the service does
        foreach (var articleInFolder in this.ArticleDB.ListArticlesForLocalFolder(folder.LocalId))
        {
            this.ArticleDB.DeleteArticle(articleInFolder.Id);
        }

        this.FolderDB.DeleteFolder(folder.LocalId);
    }

    public async Task<IEnumerable<IInstapaperFolder>> ListAsync()
    {
        await this.MaybeDelay();
        var localFolders = this.FolderDB.ListAllCompleteUserFolders();

        return localFolders.Select((f) => f.ToInstapaperFolder());
    }
    #endregion
}