using System;
namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Sorts smaller IDs higher in the list, larger lower
/// </summary>
internal sealed class OldestToNewestArticleComparer : IComparer<DatabaseArticle?>
{
    public int Compare(DatabaseArticle? x, DatabaseArticle? y)
    {
        if (x is null && y is null)
        {
            return 0;
        }

        if (x is null && y is not null)
        {
            return -1;
        }

        if (y is null && x is not null)
        {
            return 1;
        }

        return x!.Id.CompareTo(y!.Id);
    }
}

/// <summary>
/// Sorts smaller IDs lower in the list, larger higher
/// </summary>
internal sealed class NewestToOldestArticleComparer : IComparer<DatabaseArticle?>
{
    public int Compare(DatabaseArticle? x, DatabaseArticle? y)
    {
        if (x is null && y is null)
        {
            return 0;
        }

        if (x is null && y is not null)
        {
            return 1;
        }

        if (y is null && x is not null)
        {
            return -1;
        }

        // We want a descending sort.
        return (x!.Id.CompareTo(y!.Id) * -1);
    }
}

/// <summary>
/// Sort articles by descending read progress. If progress matches, place older
/// articles higher in the order
/// </summary>
internal sealed class ByProgressDescendingComparer : IComparer<DatabaseArticle>
{
    public int Compare(DatabaseArticle? x, DatabaseArticle? y)
    {
        if (x is null & y is null)
        {
            return 0;
        }

        if (x is null && y is not null)
        {
            return 1;
        }

        if (y is null && x is not null)
        {
            return -1;
        }

        // We need to dig deeper if it's the same read progress
        if (x!.ReadProgress == y!.ReadProgress)
        {
            var relativeOrder = x.ReadProgressTimestamp.CompareTo(y.ReadProgressTimestamp);

            // And even deeper if it's the same Timestamp
            if (relativeOrder == 0)
            {
                // Welp, lets hope they're different IDs!
                return (x.Id.CompareTo(y.Id));
            }

            return relativeOrder;
        }

        // -1 to make sure higher progress is sorted to the top of the list
        return (x.ReadProgress.CompareTo(y.ReadProgress) * -1);
    }
}
