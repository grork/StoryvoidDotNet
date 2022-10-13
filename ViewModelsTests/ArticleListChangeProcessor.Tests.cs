using System.Collections.ObjectModel;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleListChangeProcessorTests
{
    private readonly OldestToNewestArticleComparer OldestToNewest = new OldestToNewestArticleComparer();
    private readonly NewestToOldestArticleComparer NewestToOldest = new NewestToOldestArticleComparer();
    private readonly ByProgressDescendingComparer ProgressDescending = new ByProgressDescendingComparer();
    private readonly DatabaseEventClearingHouse clearingHouse = new DatabaseEventClearingHouse();

    private static IList<DatabaseArticle> GetSortedArticleListFor(IComparer<DatabaseArticle> sort)
    {
        var list = new List<DatabaseArticle>();
        var progress = 0.01F;
        foreach (var iteration in Enumerable.Range(1, 10))
        {
            // Discard 5
            foreach (var _ in Enumerable.Range(1, 5))
            {
                TestUtilities.GetMockDatabaseArticle();
            }

            list.Add(TestUtilities.GetMockDatabaseArticle() with { ReadProgress = progress });
            progress += 0.05F;
        }

        list.Sort(sort);

        return list;
    }

    #region Adding Articles
    [Fact]
    public void NewItemWithEmptyListIsAdded()
    {
        var sort = this.OldestToNewest;
        var articles = new List<DatabaseArticle>();

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToAdd = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleAdded(articleToAdd, WellKnownLocalFolderIds.Unread);

        Assert.Single(articles);
        Assert.Equal(articleToAdd, articles.First());
    }

    [Fact]
    public void NewArticleItemAddedAtEnd()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToAdd = TestUtilities.GetMockDatabaseArticle() with { Id = articles.Max((a) => a.Id) + 1 };

        this.clearingHouse.RaiseArticleAdded(articleToAdd, WellKnownLocalFolderIds.Unread);

        Assert.Equal(priorCount + 1, articles.Count);
        Assert.Equal(articleToAdd, articles.Last());
    }

    [Fact]
    public void MediumArticleInsertedInMiddle()
    {
        var sort = this.OldestToNewest;
        var baseArticles = GetSortedArticleListFor(sort);
        var priorCount = baseArticles.Count;

        for (var index = 0; index < baseArticles.Count; index += 1)
        {
            var articles = new List<DatabaseArticle>(baseArticles);
            using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
            var articleToAdd = articles[index] with { Id = articles[index].Id + 1 };

            this.clearingHouse.RaiseArticleAdded(articleToAdd, WellKnownLocalFolderIds.Unread);

            Assert.Equal(priorCount + 1, articles.Count);
            Assert.Equal(articleToAdd, articles[index + 1]);
        }
    }

    [Fact]
    public void OldestArticleAddedAtStart()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToAdd = TestUtilities.GetMockDatabaseArticle() with { Id = articles.Min((a) => a.Id) - 1 };

        this.clearingHouse.RaiseArticleAdded(articleToAdd, WellKnownLocalFolderIds.Unread);

        Assert.Equal(priorCount + 1, articles.Count);
        Assert.Equal(articleToAdd, articles.First());
    }
    #endregion

    #region Updating Articles
    [Fact]
    public void UpdatedArticleInSingleArticleList()
    {
        var sort = this.ProgressDescending;
        var articles = new List<DatabaseArticle> { TestUtilities.GetMockDatabaseArticle() };

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleUpdated = articles.First() with { ReadProgress = 0.0F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Single(articles);
        Assert.Equal(articleUpdated, articles.First());
    }

    [Fact]
    public void UpdatedArticleInEmptyArticleList()
    {
        var sort = this.ProgressDescending;
        var articles = new List<DatabaseArticle>();

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleUpdated = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Empty(articles);
    }

    [Fact]
    public void UpdatedArticleNotInSetIsIgnored()
    {
        var sort = this.ProgressDescending;
        var originalArticle = TestUtilities.GetMockDatabaseArticle();
        var articles = new List<DatabaseArticle> { originalArticle };

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleUpdated = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Single(articles);
        Assert.Equal(originalArticle, articles.First());
    }

    [Fact]
    public void UpdatedArticleMovesFromFirstToLast()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleUpdated = articles.First() with { ReadProgress = 0.0F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles.Last());
    }

    [Fact]
    public void UpdatedArticleMovesFromLastToFirst()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleUpdated = articles.Last() with { ReadProgress = 1.0F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles.First());
    }

    [Fact]
    public void UpdatedArticleMovesFromMiddleToDifferentMiddleHigher()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);

        var articleUpdated = halfWayItem with { ReadProgress = articles.First().ReadProgress - 0.015F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles[1]);
    }

    [Fact]
    public void UpdatedArticleMovesFromMiddleToDifferentMiddleLower()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);

        var articleUpdated = halfWayItem with { ReadProgress = articles.Last().ReadProgress + 0.015F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles[articles.Count - 2]);
    }

    [Fact]
    public void UpdatedArticleMovesFromMiddleToDifferentMiddleWithObservableCollection()
    {
        var sort = this.ProgressDescending;
        var articles = new ObservableCollection<DatabaseArticle>(GetSortedArticleListFor(sort));
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);

        var articleUpdated = halfWayItem with { ReadProgress = articles.First().ReadProgress - 0.015F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles[1]);
    }

    [Fact]
    public void UpdatedArticleMovesFromMiddleToLast()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);

        var articleUpdated = halfWayItem with { ReadProgress = 0.0F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles.Last());
    }

    [Fact]
    public void UpdatedArticleMovesFromMiddleToFirst()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);

        var articleUpdated = halfWayItem with { ReadProgress = 1.0F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles.First());
    }

    [Fact]
    public void UpdatedArticleMovesFromFirstToMiddle()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;

        var articleUpdated = articles.First() with { ReadProgress = halfWayProgress };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Contains(articleUpdated, articles);
        Assert.NotEqual(articleUpdated, articles.First());
        Assert.NotEqual(articleUpdated, articles.Last());
    }

    [Fact]
    public void UpdatedArticleMovesFromLastToMiddle()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;

        var articleUpdated = articles.Last() with { ReadProgress = halfWayProgress };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Contains(articleUpdated, articles);
        Assert.NotEqual(articleUpdated, articles.First());
        Assert.NotEqual(articleUpdated, articles.Last());
    }

    [Fact]
    public void UpdatedArticleWithChangeInNonSortedPropertyDoesntMove()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);
        var halfWayItemIndex = articles.IndexOf(halfWayItem);

        var articleUpdated = halfWayItem with { Liked = true };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles[halfWayItemIndex]);
    }

    [Fact]
    public void UpdatedArticleWithChangeInSortedPropertyButShouldStayInSameLocationDoesntMove()
    {
        var sort = this.ProgressDescending;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var halfWayProgress = articles.Max((a) => a.ReadProgress) / 2;
        var halfWayItem = articles.First((a) => a.ReadProgress <= halfWayProgress);
        var halfWayItemIndex = articles.IndexOf(halfWayItem);

        var articleUpdated = halfWayItem with { ReadProgress = halfWayItem.ReadProgress + 0.001F };

        this.clearingHouse.RaiseArticleUpdated(articleUpdated);

        Assert.Equal(priorCount, articles.Count);
        Assert.Equal(articleUpdated, articles[halfWayItemIndex]);
    }
    #endregion

    #region Deleting Articles
    [Fact]
    public void DeletingFirstExistingArticleRemovesArticleAtStart()
    {
        var sort = this.NewestToOldest;
        var articles = GetSortedArticleListFor(sort);
        var originalCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToDelete = articles.First();

        this.clearingHouse.RaiseArticleDeleted(articleToDelete.Id);

        Assert.Equal(originalCount - 1, articles.Count);
        Assert.DoesNotContain(articleToDelete, articles);
    }

    [Fact]
    public void DeletingLastExistingArticleRemovesArticleAtEnd()
    {
        var sort = this.NewestToOldest;
        var articles = GetSortedArticleListFor(sort);
        var originalCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToDelete = articles.Last();

        this.clearingHouse.RaiseArticleDeleted(articleToDelete.Id);

        Assert.Equal(originalCount - 1, articles.Count);
        Assert.DoesNotContain(articleToDelete, articles);
    }

    [Fact]
    public void DeletingMiddleExistingArticleRemovesArticleInMiddle()
    {
        var sort = this.NewestToOldest;
        var articles = GetSortedArticleListFor(sort);
        var originalCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToDelete = articles[4];

        this.clearingHouse.RaiseArticleDeleted(articleToDelete.Id);

        Assert.Equal(originalCount - 1, articles.Count);
        Assert.DoesNotContain(articleToDelete, articles);
    }

    [Fact]
    public void DeletedArticleEventForArticleNotInTheListIsIgnored()
    {
        var sort = this.NewestToOldest;
        var articles = GetSortedArticleListFor(sort);
        var originalCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);

        this.clearingHouse.RaiseArticleDeleted(999L);

        Assert.Equal(originalCount, articles.Count);
    }

    [Fact]
    public void DeletedArticleEventWithEmptyListDoesNothing()
    {
        var sort = this.NewestToOldest;
        var articles = new List<DatabaseArticle>();
        var originalCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);

        this.clearingHouse.RaiseArticleDeleted(999L);

        Assert.Equal(originalCount, articles.Count);
    }

    [Fact]
    public void DeletingLastArticleInTheListDeletesIt()
    {
        var sort = this.NewestToOldest;
        var articles = new List<DatabaseArticle> { TestUtilities.GetMockDatabaseArticle() };

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToDelete = articles.First();

        this.clearingHouse.RaiseArticleDeleted(articleToDelete.Id);

        Assert.Empty(articles);
    }
    #endregion

    #region Moved Articles
    [Fact]
    public void ArticleMovedIntoEmptyFolder()
    {
        var sort = this.OldestToNewest;
        var articles = new List<DatabaseArticle>();

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMoved = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleMoved(articleToMoved, WellKnownLocalFolderIds.Unread);

        Assert.Single(articles);
        Assert.Equal(articleToMoved, articles.First());
    }

    [Fact]
    public void ArticleMovedIntoFolderFirst()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = TestUtilities.GetMockDatabaseArticle() with { Id = articles.Min((a) => a.Id) - 1 };

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Unread);

        Assert.Equal(priorCount + 1, articles.Count);
        Assert.Equal(articleToMove, articles.First());
    }

    [Fact]
    public void ArticleMovedIntoFolderMiddle()
    {
        var sort = this.OldestToNewest;
        var baseArticles = GetSortedArticleListFor(sort);
        var priorCount = baseArticles.Count;

        for (var index = 0; index < baseArticles.Count; index += 1)
        {
            var articles = new List<DatabaseArticle>(baseArticles);
            using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
            var articleToMove = articles[index] with { Id = articles[index].Id + 1 };

            this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Unread);

            Assert.Equal(priorCount + 1, articles.Count);
            Assert.Equal(articleToMove, articles[index + 1]);
        }
    }

    [Fact]
    public void ArticleMovedIntoFolderLast()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = TestUtilities.GetMockDatabaseArticle() with { Id = articles.Max((a) => a.Id) + 1 };

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Unread);

        Assert.Equal(priorCount + 1, articles.Count);
        Assert.Equal(articleToMove, articles.Last());
    }

    [Fact]
    public void ArticleMovedInAnotherFolderAndNotToThisFolder()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Archive);

        Assert.Equal(priorCount, articles.Count);
        Assert.DoesNotContain(articleToMove, articles);
    }

    [Fact]
    public void ArticleMovedInAnotherFolderAndNotToThisFolderWhichIsEmpty()
    {
        var sort = this.OldestToNewest;
        var articles = new List<DatabaseArticle>();

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = TestUtilities.GetMockDatabaseArticle();

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Archive);

        Assert.Empty(articles);
    }

    [Fact]
    public void ArticleMovedOutOfThisFolder()
    {
        var sort = this.OldestToNewest;
        var articles = GetSortedArticleListFor(sort);
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = articles.First();

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Archive);

        Assert.Equal(priorCount - 1, articles.Count);
        Assert.DoesNotContain(articleToMove, articles);
    }

    [Fact]
    void ArticleMovedOutOfThisFolderLeavingItEmpty()
    {
        var sort = this.OldestToNewest;
        var articles = new List<DatabaseArticle> { TestUtilities.GetMockDatabaseArticle() };
        var priorCount = articles.Count;

        using var changeHandler = new ArticleListChangeProcessor(articles, WellKnownLocalFolderIds.Unread, this.clearingHouse, sort);
        var articleToMove = articles.First();

        this.clearingHouse.RaiseArticleMoved(articleToMove, WellKnownLocalFolderIds.Archive);

        Assert.Empty(articles);
    }
    #endregion
}