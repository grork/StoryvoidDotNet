using System.Data;
using Codevoid.Instapaper;
using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

internal static class MockBookmarkExtensions
{
    internal static IInstapaperBookmark ToInstapaperBookmark(this DatabaseArticle instance)
    {
        return new MockBookmark()
        {
            Id = instance.Id,
            Url = instance.Url,
            Title = instance.Title,
            Description = instance.Description,
            Progress = instance.ReadProgress,
            ProgressTimestamp = instance.ReadProgressTimestamp,
            Liked = instance.Liked,
            Hash = instance.Hash
        };
    }

    internal static IList<DatabaseArticle> ListAllArticles(this IArticleDatabase instance)
    {
        var allArticles = new List<DatabaseArticle>();
        allArticles.AddRange(instance.ListAllArticlesInAFolder().Select((a) => a.Article));
        allArticles.AddRange(instance.ListArticlesNotInAFolder());

        return allArticles;
    }
}

internal class MockBookmark : IInstapaperBookmark
{
    public long Id { get; set; } = 0L;
    public Uri Url { get; set; } = new Uri("unset://unset");
    public string Title { get; set; } = String.Empty;
    public string Description { get; set; } = String.Empty;
    public float Progress { get; set; } = 0.0F;
    public DateTime ProgressTimestamp { get; set; } = DateTime.MinValue;
    public bool Liked { get; set; } = false;
    public string Hash { get; set; } = String.Empty;
}

public class MockBookmarksService : IBookmarksClient
{
    private long nextServiceId = 1L;
    internal IArticleDatabase ArticleDB { get; init; }
    private IFolderDatabase folderDb;

    internal MockBookmarksService(IArticleDatabase articleDb, IFolderDatabase folderDb)
    {
        this.ArticleDB = articleDb;
        this.folderDb = folderDb;

        var allArticles = articleDb.ListAllArticles();

        if (allArticles.Count > 0)
        {
            var max = (from a in allArticles
                       select a.Id).Max();
            this.nextServiceId = max + 1;
        }
    }

    private long GetNextServiceId()
    {
        return Interlocked.Increment(ref this.nextServiceId);
    }

    public Task<IInstapaperBookmark> AddAsync(Uri bookmarkUrl, AddBookmarkOptions? options)
    {
        // Handle the special case of an article *moving* to unread. The service
        // handles this by making us add it again, which implicitly moves it to
        // the unread folder. So, detect if we already have that URL, and then
        // move it to unread.
        var existingArticle = this.ArticleDB.ListAllArticles().FirstOrDefault((a) => a.Url == bookmarkUrl);
        if(existingArticle is not null)
        {
            this.ArticleDB.MoveArticleToFolder(existingArticle.Id, WellKnownLocalFolderIds.Unread);
            return Task.FromResult(existingArticle.ToInstapaperBookmark());
        }

        var nextId = GetNextServiceId();
        var localFolderId = WellKnownLocalFolderIds.Unread;

        // If there was a specific folder we want to add to, we need to look it
        // up by the service ID first.
        if(options?.DestinationFolderId > 0L)
        {
            var folder = this.folderDb.GetFolderByServiceId(options.DestinationFolderId)!;
            localFolderId = folder.LocalId;
        }

        var article = this.ArticleDB.AddArticleToFolder(new (
            nextId,
            title: (options is not null) ? options.Title : nextId.ToString(),
            url: bookmarkUrl,
            description: (options is not null) ? options.Description : String.Empty,
            readProgress: 0.0F,
            readProgressTimestamp: DateTime.Now,
            hash: "1234",
            liked: false
        ), localFolderId);

        return Task.FromResult(article.ToInstapaperBookmark());
    }

    public Task<IInstapaperBookmark> ArchiveAsync(long bookmark_id)
    {
        var existingBookmark = this.ArticleDB.GetArticleById(bookmark_id);
        if(existingBookmark is null)
        {
            throw new EntityNotFoundException();
        }
        
        this.ArticleDB.MoveArticleToFolder(bookmark_id, WellKnownLocalFolderIds.Archive);
        return Task.FromResult(existingBookmark.ToInstapaperBookmark());
    }

    public Task DeleteAsync(long bookmark_id)
    {
        this.ArticleDB.DeleteArticle(bookmark_id);
        return Task.CompletedTask;
    }

    public Task<string> GetTextAsync(long bookmark_id)
    {
        throw new NotImplementedException();
    }

    public Task<IInstapaperBookmark> LikeAsync(long bookmark_id)
    {
        throw new NotImplementedException();
    }

    public Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(string folderId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit)
    {
        throw new NotImplementedException();
    }

    public Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(long folderId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit)
    {
        throw new NotImplementedException();
    }

    public Task<IInstapaperBookmark> MoveAsync(long bookmark_id, long folder_id)
    {
        var destinationFolder = this.folderDb.GetFolderByServiceId(folder_id);
        var article = this.ArticleDB.GetArticleById(bookmark_id);
        if(destinationFolder is null || article is null)
        {
            throw new EntityNotFoundException();
        }

        this.ArticleDB.MoveArticleToFolder(bookmark_id, destinationFolder.LocalId);

        return Task.FromResult(article.ToInstapaperBookmark());
    }

    public Task<IInstapaperBookmark> UnarchiveAsync(long bookmark_id)
    {
        throw new NotImplementedException();
    }

    public Task<IInstapaperBookmark> UnlikeAsync(long bookmark_id)
    {
        throw new NotImplementedException();
    }

    public Task<IInstapaperBookmark> UpdateReadProgressAsync(long bookmark_id, float progress, DateTime progress_timestamp)
    {
        throw new NotImplementedException();
    }
}