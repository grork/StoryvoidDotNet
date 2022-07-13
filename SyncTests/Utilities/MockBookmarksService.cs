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

    internal static IDictionary<long, HaveStatus> ToDictionary(this IEnumerable<HaveStatus> instance)
    {
        var result = new Dictionary<long, HaveStatus>();
        foreach(var status in instance)
        {
            result.Add(status.Id, status);
        }

        return result;
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

    private long ServiceFolderIdToLocalFolderId(string folderId)
    {
        switch (folderId)
        {
            case WellKnownFolderIds.Unread:
                return WellKnownLocalFolderIds.Unread;

            case WellKnownFolderIds.Archived:
                return WellKnownLocalFolderIds.Archive;

            default:
                var serviceId = Int64.Parse(folderId);
                var folder = this.folderDb.GetFolderByServiceId(serviceId);
                if(folder is null)
                {
                    throw new EntityNotFoundException();
                }
                
                return folder.LocalId;
        }
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
        try
        {
            this.ArticleDB.LikeArticle(bookmark_id);
            var bookmark = this.ArticleDB.GetArticleById(bookmark_id)!.ToInstapaperBookmark();
            return Task.FromResult(bookmark);
        }
        catch(ArticleNotFoundException)
        {
            throw new EntityNotFoundException();
        }
    }

    public Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(string folderServiceId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit)
    {
        var limit = Convert.ToInt32(resultLimit);
        IList<DatabaseArticle> serviceArticles = new List<DatabaseArticle>();
        if (folderServiceId == WellKnownFolderIds.Liked)
        {
            serviceArticles = this.ArticleDB.ListLikedArticles();
        }
        else
        {
            var localFolderId = ServiceFolderIdToLocalFolderId(folderServiceId);
            serviceArticles = this.ArticleDB.ListArticlesForLocalFolder(localFolderId);
        }

        serviceArticles = serviceArticles.OrderBy((a) => a.Id).Take(limit).ToList();

        IList<IInstapaperBookmark> result = new List<IInstapaperBookmark>();
        IList<long> deletes = new List<long>();

        if(haveInformation is null)
        {
            // If there was no have information, we should just return the
            // contents of the folder
            return Task.FromResult<(IList<IInstapaperBookmark>, IList<long>)>(
                (serviceArticles.Select((a) => a.ToInstapaperBookmark()).Take(limit).ToList(), deletes)
            );
        }

        var havesMap = haveInformation.ToDictionary();

        // Check for any that we're aware of
        foreach(var serviceArticle in serviceArticles)
        {
            // If the article from the DB didn't have, uhh, have information
            // in the request then it must be new to the client, so included
            // it in the response. No more processing needed.
            if(!havesMap.ContainsKey(serviceArticle.Id))
            {
                result.Add(serviceArticle.ToInstapaperBookmark());
                continue;
            }

            var have = havesMap[serviceArticle.Id];

            // We've processed this have now, so remove it. Any left over will
            // be deletes
            havesMap.Remove(serviceArticle.Id);

            if(String.IsNullOrWhiteSpace(have.Hash))
            {
                // If we don't have a hash, but did have the ID in the have, we
                // can only do included/excluded decisions. Since it *was* known
                // to the client, we don't include it
                continue;
            }

            // Same hash implies no change
            if(serviceArticle.Hash == have.Hash)
            {
                continue;
            }

            // Hash is different so we need to include the article. But we also
            // need to update the hash if we're updating our own information
            var updatedArticle = serviceArticle;
            if(serviceArticle.ReadProgressTimestamp < have.ProgressLastChanged)
            {
                updatedArticle = updatedArticle with
                {
                    ReadProgressTimestamp = have.ProgressLastChanged!.Value,
                    ReadProgress = have.ReadProgress!.Value,
                    Hash = have.ProgressLastChanged!.Value.ToString()
                };

                // Update the database with the information
                updatedArticle = this.ArticleDB.UpdateArticle(new ArticleRecordInformation(
                    id: updatedArticle.Id,
                    title: updatedArticle.Title,
                    url: updatedArticle.Url,
                    description: updatedArticle.Description,
                    readProgress: updatedArticle.ReadProgress,
                    readProgressTimestamp: updatedArticle.ReadProgressTimestamp,
                    hash: updatedArticle.Hash,
                    liked: updatedArticle.Liked
                ));
            }

            result.Add(updatedArticle.ToInstapaperBookmark());
        }

        deletes = new List<long>(havesMap.Select((kvp) => kvp.Key));
        return Task.FromResult((result, deletes));
    }

    public Task<(IList<IInstapaperBookmark> Bookmarks, IList<long> DeletedIds)> ListAsync(long folderId, IEnumerable<HaveStatus>? haveInformation, uint resultLimit)
    {
        return this.ListAsync(folderId.ToString(), haveInformation, resultLimit);
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
        try
        {
            this.ArticleDB.UnlikeArticle(bookmark_id);
            var bookmark = this.ArticleDB.GetArticleById(bookmark_id)!.ToInstapaperBookmark();
            return Task.FromResult(bookmark);
        }
        catch(ArticleNotFoundException)
        {
            throw new EntityNotFoundException();
        }
    }

    public Task<IInstapaperBookmark> UpdateReadProgressAsync(long bookmark_id, float progress, DateTime progress_timestamp)
    {
        throw new NotImplementedException();
    }
}