using System.Collections.ObjectModel;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleListChangeProcessorTests
{
    private readonly OldestToNewestArticleComparer OldestToNewest = new OldestToNewestArticleComparer();
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
        IList<DatabaseArticle> articles = new List<DatabaseArticle>();

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
        IList<DatabaseArticle> articles = new List<DatabaseArticle> { TestUtilities.GetMockDatabaseArticle() };

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
        IList<DatabaseArticle> articles = new List<DatabaseArticle>();

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
        IList<DatabaseArticle> articles = new List<DatabaseArticle> { originalArticle };

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
}