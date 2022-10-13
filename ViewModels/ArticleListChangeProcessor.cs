using System;
using System.Collections.ObjectModel;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Listens for changes from an <see cref="IDatabaseEventSink" /> and applies
/// them to the provided list, respecting the provided sort
/// </summary>
public class ArticleListChangeProcessor : BaseListChangeProcessor<DatabaseArticle>, IDisposable
{
    private readonly IDatabaseEventSink eventSource;
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
        IComparer<DatabaseArticle> sort) : base(list, sort)
    {
        this.eventSource = eventSource;
        this.targetFolderLocalId = localFolderId;

        this.StartListeningForArticleChanges();
    }

    private void StartListeningForArticleChanges()
    {
        this.eventSource.ArticleAdded += this.HandleArticleAdded;
        this.eventSource.ArticleDeleted += this.HandleArticleDeleted;
        this.eventSource.ArticleUpdated += this.HandleArticleUpdated;
        this.eventSource.ArticleMoved += this.HandleArticleMoved;
    }

    private void StopListeningForArticleChanges()
    {
        this.eventSource.ArticleAdded -= this.HandleArticleAdded;
        this.eventSource.ArticleDeleted -= this.HandleArticleDeleted;
        this.eventSource.ArticleUpdated -= this.HandleArticleUpdated;
        this.eventSource.ArticleMoved -= this.HandleArticleMoved;
    }

    public void Dispose()
    {
        this.StopListeningForArticleChanges();
    }

    protected override bool IdentifiersMatch(DatabaseArticle first, DatabaseArticle second) => first.Id == second.Id;
    protected override bool IdentifiersMatch(DatabaseArticle item, long identifier) => item.Id == identifier;

    private void HandleArticleAdded(object? sender, (DatabaseArticle Article, long LocalFolderId) e)
    {
        // Drop changes if they're not for the folder we're monitoring
        if (e.LocalFolderId != targetFolderLocalId)
        {
            return;
        }

        this.HandleItemAdded(e.Article);
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

    private void HandleArticleDeleted(object? sender, long e) => this.HandleItemDeleted(e);
    private void HandleArticleUpdated(object? sender, DatabaseArticle e) => this.HandleItemUpdated(e);
}
