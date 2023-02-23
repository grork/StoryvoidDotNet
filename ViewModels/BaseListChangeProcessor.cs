using System.Collections.ObjectModel;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Base class to provide common implementation of list changes. Note, this is
/// not specific to an item type, and thus needs to be derived to ensure that
/// the right handler is called in the right situation.
/// </summary>
/// <typeparam name="T">List type, and items being sorted</typeparam>
public abstract class BaseListChangeProcessor<T>
{
    protected readonly IList<T> targetList;
    private readonly IComparer<T> comparer;

    protected BaseListChangeProcessor(IList<T> targetList, IComparer<T> comparer)
    {
        this.targetList = targetList;
        this.comparer = comparer;
    }

    /// <summary>
    /// Compares two items by their (stable) identifiers.
    /// </summary>
    /// <param name="first">First item to compare</param>
    /// <param name="second">Second item to repair</param>
    /// <returns>True if they match, false otherwise</returns>
    protected abstract bool IdentifiersMatch(T first, T second);

    /// <summary>
    /// Compares an item to a supplied identifier value
    /// </summary>
    /// <param name="item">Item that the identifier is being checked on</param>
    /// <param name="identifier">The identifier to compare to</param>
    /// <returns>True if the identifier matches the item, false otherwise</returns>
    protected abstract bool IdentifiersMatch(T item, long identifier);

    /// <summary>
    /// When an item is 'added', it will be inserted at the sorted index as
    /// determined by the <see cref="comparer"/> instance
    /// </summary>
    /// <param name="addedItem">Added item</param>
    protected void HandleItemAdded(T addedItem)
    {
        // Special case an empty list -- nothing to sort, so just add it.
        if (this.targetList.Count == 0)
        {
            this.targetList.Add(addedItem);
            return;
        }

        // Special case the last item, because new items will end up at the
        // end of the list anyway, and we don't have to inspect the items if it
        // would be placed at the end anyway.
        if (this.comparer.Compare(addedItem, this.targetList[this.targetList.Count - 1]) == 1)
        {
            this.targetList.Add(addedItem);
            return;
        }

        // Loop through the list until we find a position that we're going to
        // insert at.
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];
            var relativeOrder = this.comparer.Compare(addedItem, item);

            // If existing item is equal to, or greater than the new one, we
            // haven't found a place to add it, so go around again
            if (relativeOrder > -1)
            {
                continue;
            }

            this.targetList.Insert(index, addedItem);
            break;
        }
    }

    /// <summary>
    /// Handles when an item that might be in the list is mutated in someway
    /// that might result in a different position in the list per the sort.
    /// </summary>
    /// <param name="updatedItem">Updated item</param>
    protected void HandleItemUpdated(T updatedItem)
    {
        var originalIndex = -1;
        var targetIndex = -1;

        // Item won't be in the list if it's an empty list
        if (this.targetList.Count == 0)
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
            if (this.IdentifiersMatch(item, updatedItem))
            {
                originalIndex = index;
            }

            // Check if this is a new location for the item
            var relativeOrder = this.comparer.Compare(updatedItem, item);
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
            targetIndex = this.targetList.Count - 1;
        }

        // For observable lists, we don't want to raise a delete then add
        // operation as it might cause the UI to flicker -- we'd rather a nice
        // move animation is played. Applying <c>Move</c> in that scenario gets
        // us that behaviour.
        var asObservable = this.targetList as ObservableCollection<T>;
        if (asObservable is not null)
        {
            asObservable[originalIndex] = updatedItem;
            if (originalIndex != targetIndex)
            {
                asObservable.Move(originalIndex, targetIndex);
            }
            return;
        }

        this.targetList.RemoveAt(originalIndex);
        this.targetList.Insert(targetIndex, updatedItem);
    }

    /// <summary>
    /// When an item identifier is removed from the list, this will locate the
    /// item by identifier, and remove it from the list if present.
    /// </summary>
    /// <param name="identifierOfRemovedItem"></param>
    protected void HandleItemDeleted(long identifierOfRemovedItem)
    {
        // We only have the index of the deleted item, so we need to find its
        // index rather than just 'Remove'.
        var indexOfItemToRemove = -1;
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];
            if (this.IdentifiersMatch(item, identifierOfRemovedItem))
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
}