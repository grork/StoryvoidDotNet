using System;
namespace Codevoid.Storyvoid.ViewModels;

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

        if (x!.ReadProgress == y!.ReadProgress)
        {
            return x.ReadProgressTimestamp.CompareTo(y.ReadProgressTimestamp);
        }

        return (x.ReadProgress.CompareTo(y.ReadProgress) * -1);
    }
}
