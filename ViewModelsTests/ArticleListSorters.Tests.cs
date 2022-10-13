using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class OldestToNewestArticleComparerTests
{
    private IComparer<DatabaseArticle?> comparer = new OldestToNewestArticleComparer();

    [Fact]
    public void TwoNullsReturnZero()
    {
        Assert.Equal(0, comparer.Compare(null, null));
    }

    [Fact]
    public void FirstArticleValidSecondNullSortsNullFirst()
    {
        DatabaseArticle? firstArticle = null;
        var secondArticle = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(-1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleNullSecondValidReturnsNullFirst()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle();
        DatabaseArticle? secondArticle = null;
        Assert.Equal(1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleOlderThanSecondReturnsFirstFirst()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(-1, comparer.Compare(first, second));
    }

    [Fact]
    public void SecondArticleOlderThanFirstReturnsSecondFirst()
    {
        var second = TestUtilities.GetMockDatabaseArticle();
        var first = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(1, comparer.Compare(first, second));
    }

    [Fact]
    public void SecondArticleSameAsFirstReturnsSameOrder()
    {
        var second = TestUtilities.GetMockDatabaseArticle();
        var first = second with { Liked = true };
        Assert.Equal(0, comparer.Compare(first, second));
    }

    [Fact]
    public void ListSortsOldestFirst()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        var third = TestUtilities.GetMockDatabaseArticle();
        var fourth = TestUtilities.GetMockDatabaseArticle();

        var listOfArticles = new List<DatabaseArticle>
        {
            fourth,
            third,
            second,
            first
        };

        listOfArticles.Sort(this.comparer);

        Assert.Equal(first, listOfArticles.First());
    }

    [Fact]
    public void ListSortsNullFirst()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        var third = TestUtilities.GetMockDatabaseArticle();
        var fourth = TestUtilities.GetMockDatabaseArticle();
        DatabaseArticle? fifth = null;

        var listOfArticles = new List<DatabaseArticle?>
        {
            fifth,
            fourth,
            third,
            second,
            first
        };

        listOfArticles.Sort(this.comparer);

        Assert.Null(listOfArticles.First());
    }
}

public class NewestToOldestArticleComparerTests
{
    private IComparer<DatabaseArticle?> comparer = new NewestToOldestArticleComparer();

    [Fact]
    public void TwoNullsReturnZero()
    {
        Assert.Equal(0, comparer.Compare(null, null));
    }

    [Fact]
    public void FirstArticleValidSecondNullSortsNullSecond()
    {
        DatabaseArticle? firstArticle = null;
        var secondArticle = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleNullSecondValidReturnsNullSecond()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle();
        DatabaseArticle? secondArticle = null;
        Assert.Equal(-1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleOlderThanFirstReturnsSecondFirst()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(1, comparer.Compare(first, second));
    }

    [Fact]
    public void SecondArticleOlderThanFirstReturnsSecondSecond()
    {
        var second = TestUtilities.GetMockDatabaseArticle();
        var first = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(-1, comparer.Compare(first, second));
    }

    [Fact]
    public void SecondArticleSameAsFirstReturnsSameOrder()
    {
        var second = TestUtilities.GetMockDatabaseArticle();
        var first = second with { Liked = true };
        Assert.Equal(0, comparer.Compare(first, second));
    }

    [Fact]
    public void ListSortsOldestFirst()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        var third = TestUtilities.GetMockDatabaseArticle();
        var fourth = TestUtilities.GetMockDatabaseArticle();

        var listOfArticles = new List<DatabaseArticle>
        {
            first,
            second,
            third,
            fourth
        };

        listOfArticles.Sort(this.comparer);

        Assert.Equal(fourth, listOfArticles.First());
    }

    [Fact]
    public void ListSortsNullLast()
    {
        var first = TestUtilities.GetMockDatabaseArticle();
        var second = TestUtilities.GetMockDatabaseArticle();
        var third = TestUtilities.GetMockDatabaseArticle();
        var fourth = TestUtilities.GetMockDatabaseArticle();
        DatabaseArticle? fifth = null;

        var listOfArticles = new List<DatabaseArticle?>
        {
            fifth,
            first,
            second,
            third,
            fourth
        };

        listOfArticles.Sort(this.comparer);

        Assert.Null(listOfArticles.Last());
    }
}

public class ByProgressDescendingComparerTest
{
    private IComparer<DatabaseArticle> comparer = new ByProgressDescendingComparer();

    [Fact]
    public void TwoNullsReturnZero()
    {
        Assert.Equal(0, comparer.Compare(null, null));
    }

    [Fact]
    public void FirstArticleValidSecondNullSortsNullSecond()
    {
        DatabaseArticle? firstArticle = null;
        var secondArticle = TestUtilities.GetMockDatabaseArticle();
        Assert.Equal(1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleNullSecondValidReturnsNullSecond()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle();
        DatabaseArticle? secondArticle = null;
        Assert.Equal(-1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void FirstArticleHasMoreProgressThanSecondAndIsReturnedFirst()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.99F
        };
        var secondArticle = TestUtilities.GetMockDatabaseArticle();

        Assert.Equal(-1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void SecondArticleHasMoreProgressThanFirstAndIsReturnedFirst()
    {
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.99F
        };
        var firstArticle = TestUtilities.GetMockDatabaseArticle();

        Assert.Equal(1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void ListSortsHigherProgressFirst()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle() with { ReadProgress = 0.9F };
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with { ReadProgress = 0.7F };
        var thirdArticle = TestUtilities.GetMockDatabaseArticle() with { ReadProgress = 0.5F };
        var fourthArticle = TestUtilities.GetMockDatabaseArticle() with { ReadProgress = 0.3F };

        var listOfArticles = new List<DatabaseArticle>
        {
            fourthArticle,
            thirdArticle,
            secondArticle,
            firstArticle
        };

        listOfArticles.Sort(this.comparer);
        Assert.Equal(firstArticle, listOfArticles.First());
    }

    [Fact]
    public void FirstArticleWithSameProgressAsSecondButOlderTimestampIsReturnedFirst()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(5))
        };

        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now
        };

        Assert.Equal(-1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void SecondArticleWithSameProgressAsFirstButOlderTimestampIsReturnedFirst()
    {
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(5))
        };

        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now
        };

        Assert.Equal(1, comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void ListSortsOlderProgressTimestampsFirst()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(5)),
        };
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(4)),
        };
        var thirdArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(3)),
        };
        var fourthArticle = TestUtilities.GetMockDatabaseArticle() with
        {
            ReadProgress = 0.5F,
            ReadProgressTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(2)),
        };

        var listOfArticles = new List<DatabaseArticle>
        {
            fourthArticle,
            thirdArticle,
            secondArticle,
            firstArticle
        };

        listOfArticles.Sort(this.comparer);
        Assert.Equal(firstArticle, listOfArticles.First());
    }

    [Fact]
    public void SameProgressDifferentIDSortsByOldestFirst()
    {
        (float progress, DateTime timestamp) p = (0.5F, DateTime.Now);
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp };

        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp };

        Assert.Equal(1, this.comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void SameProgressSameIDSamePosition()
    {
        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = 0.5F, ReadProgressTimestamp = DateTime.Now };

        var secondArticle = firstArticle with { Liked = true };

        Assert.Equal(0, this.comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void SameProgressDifferentTimestampSortsOlderFirst()
    {
        (float progress, DateTime timestamp) p = (0.5F, DateTime.Now);
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp };

        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp.Subtract(TimeSpan.FromDays(1)) };

        Assert.Equal(-1, this.comparer.Compare(firstArticle, secondArticle));
    }

    [Fact]
    public void SameProgressDifferentTimestampSortsNewerLast()
    {
        (float progress, DateTime timestamp) p = (0.5F, DateTime.Now);
        var secondArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp };

        var firstArticle = TestUtilities.GetMockDatabaseArticle() with
        { ReadProgress = p.progress, ReadProgressTimestamp = p.timestamp.Add(TimeSpan.FromDays(1)) };

        Assert.Equal(1, this.comparer.Compare(firstArticle, secondArticle));
    }
}