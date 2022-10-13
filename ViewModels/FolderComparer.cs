using System;
namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Sorts folders so that they follow unread/archive/synced/local only ordering
/// </summary>
internal class FolderComparer : IComparer<DatabaseFolder>
{
    public int Compare(DatabaseFolder? x, DatabaseFolder? y)
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

        if (x!.LocalId == y!.LocalId && x!.Position == y!.Position)
        {
            return 0;
        }

        // Unread *always* sorts first
        if (x.LocalId == WellKnownLocalFolderIds.Unread)
        {
            return -1;
        }

        if (y.LocalId == WellKnownLocalFolderIds.Unread)
        {
            return 1;
        }

        // Archive *always* earlier user folders (Excl. unread)
        if (x.LocalId == WellKnownLocalFolderIds.Archive)
        {
            return -1;
        }

        if (y.LocalId == WellKnownLocalFolderIds.Archive)
        {
            return 1;
        }

        // Sort items without real positions lower in the list
        if (!x.ServiceId.HasValue && y.ServiceId.HasValue)
        {
            return 1;
        }

        if (x.ServiceId.HasValue && !y.ServiceId.HasValue)
        {
            return -1;
        }

        // If we don't have position, fallback to local ID
        if (!x.ServiceId.HasValue && !y.ServiceId.HasValue)
        {
            var relativeOrder = x.Position.CompareTo(y.Position);
            if (relativeOrder == 0)
            {
                relativeOrder = x.LocalId.CompareTo(y.LocalId);
            }

            return relativeOrder;
        }

        return x.Position.CompareTo(y.Position);
    }
}