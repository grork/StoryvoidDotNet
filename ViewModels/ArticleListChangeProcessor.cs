using System;
using System.Collections.ObjectModel;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Listens for changes from an <see cref="IDatabaseEventSink" /> and applies
/// them to the provided list, respecting the provided sort
/// </summary>
public class ArticleListChangeProcessor : IDisposable
{
    private readonly IList<DatabaseArticle> targetList;
    private readonly IDatabaseEventSink eventSource;
    private readonly IComparer<DatabaseArticle> comparer;
    private readonly long targetFolderLocalId;

    /// <summary>
    /// Construct new change processor, and start listening for changes.
    /// </summary>
    /// <param name="list">List to apply changes to</param>
    /// <param name="localFolderId">Folder ID to listen for filter changes to</param>
    /// <param name="eventSource"><see cref="IDatabaseEventSink"/> instance to listen to</param>
    /// <param name="sort">Sort to apply</param>
    public ArticleListChangeProcessor(IList<DatabaseArticle> list,
        long localFolderId,
        IDatabaseEventSink eventSource,
        IComparer<DatabaseArticle> sort)
    {
        this.targetList = list;
        this.eventSource = eventSource;
        this.comparer = sort;
        this.targetFolderLocalId = localFolderId;

        this.StartListeningForArticleChanges();
    }

    private void StartListeningForArticleChanges()
    {
        this.eventSource.ArticleAdded += HandleArticleAdded;
        this.eventSource.ArticleDeleted += HandleArticleDeleted;
        this.eventSource.ArticleUpdated += HandleArticleUpdated;
        this.eventSource.ArticleMoved += HandleArticleMoved;
    }

    private void StopListeningForArticleChanges()
    {
        this.eventSource.ArticleAdded -= HandleArticleAdded;
        this.eventSource.ArticleDeleted -= HandleArticleDeleted;
        this.eventSource.ArticleUpdated -= HandleArticleUpdated;
        this.eventSource.ArticleMoved -= HandleArticleMoved;
    }

    public void Dispose()
    {
        this.StopListeningForArticleChanges();
    }

    private void HandleArticleAdded(object? sender, (DatabaseArticle Article, long LocalFolderId) e)
    {
        // Drop changes if they're not for the folder we're monitoring
        if (e.LocalFolderId != targetFolderLocalId)
        {
            return;
        }

        // Special case an empty list -- nothing to sort, so just add it.
        if (this.targetList.Count == 0)
        {
            this.targetList.Add(e.Article);
            return;
        }

        // Special case the last item, because new articles will end up at the
        // end of the list anyway, and we don't have to inspect the items if it
        // would be placed at the end anyway.
        if (this.comparer.Compare(e.Article, this.targetList[this.targetList.Count - 1]) == 1)
        {
            this.targetList.Add(e.Article);
            return;
        }

        // Loop through the list until we find a position that we're going to
        // insert at.
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];
            var relativeOrder = this.comparer.Compare(e.Article, item);

            // If existing item is equal to, or greater than the new one, we
            // haven't found a place to add it, so go around again
            if (relativeOrder > -1)
            {
                continue;
            }

            this.targetList.Insert(index, e.Article);
            break;
        }
    }

    private void HandleArticleDeleted(object? sender, long e)
    {
        // We only have the index of the deleted item, so we need to find its
        // index rather than just 'Remove'.
        var indexOfItemToRemove = -1;
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];
            if (item.Id == e)
            {
                indexOfItemToRemove = index;
                break;
            }
        }

        // We didn't find the item in the list, so nothing to do.
        if (indexOfItemToRemove == -1)
        {
            return;
        }

        this.targetList.RemoveAt(indexOfItemToRemove);
    }

    private void HandleArticleUpdated(object? sender, DatabaseArticle e)
    {
        var originalIndex = -1;
        var targetIndex = -1;

        // Item won't be in the list if it's an empty list
        if (targetList.Count == 0)
        {
            return;
        }

        // Iterate to the end of the list. We need to go to the end, even if
        // we find an insertion position earlier 'cause we need the original
        // index to move from
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];

            // Check if the item is the one we're looking for, capturing its
            // index
            if (item.Id == e.Id)
            {
                originalIndex = index;
            }

            // Check if this is a new location for the item
            var relativeOrder = this.comparer.Compare(e, item);
            if (relativeOrder == 1)
            {
                continue;
            }

            // Once we've found an index, we don't want to keep looking for it
            // since we're assuming no duplicates.
            if (targetIndex == -1)
            {
                targetIndex = index;
            }
        }

        if (originalIndex == -1)
        {
            // Drop this event
            return;
        }

        // If we've found a target index greater than our original index, we
        // need to adjust the target index down by one, to account for the items
        // after originalIndex shuffling up the list. Without this, the item is
        // moveed one-too-many positions.
        if (originalIndex < targetIndex)
        {
            targetIndex -= 1;
        }

        // We didn't find a higher place to put this item, so we're going to
        // assume that means right at the end
        if (targetIndex == -1)
        {
            targetIndex = targetList.Count - 1;
        }

        // For observable lists, we don't want to raise a delete then add
        // operation as it might cause the UI to flicker -- we'd rather a nice
        // move animation is played. Applying <c>Move</c> in that scenario gets
        // us that behaviour.
        var asObservable = this.targetList as ObservableCollection<DatabaseArticle>;
        if (asObservable is not null)
        {
            asObservable[originalIndex] = e;
            asObservable.Move(originalIndex, targetIndex);
            return;
        }

        targetList.RemoveAt(originalIndex);
        targetList.Insert(targetIndex, e);
    }

    private void HandleArticleMoved(object? sender, (DatabaseArticle Article, long DestinationLocalFolderId) e)
    {
        // Treat 'moved to this folder' as an add; thats basically what it is
        if (e.DestinationLocalFolderId == this.targetFolderLocalId)
        {
            this.HandleArticleAdded(sender, e);
            return;
        }

        // For everything else, treat it as a delete. Since we have the article
        // we can do remove, and it'll do the right thing for us
        this.targetList.Remove(e.Article);
    }
}
