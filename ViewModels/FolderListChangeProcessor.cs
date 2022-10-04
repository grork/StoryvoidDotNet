namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Processes changes from an <see cref="IDatabaseEventSink"/> and applies them
/// to the provided list using <see cref="FolderComparer"/> to sort them
/// </summary>
public class FolderListChangeProcessor : IDisposable
{
    private readonly IList<DatabaseFolder> targetList;
    private readonly IComparer<DatabaseFolder> comparer = new FolderComparer();
    private readonly IDatabaseEventSink eventSource;

    public FolderListChangeProcessor(
        IList<DatabaseFolder> targetList,
        IDatabaseEventSink eventSource
    )
    {
        this.targetList = targetList;
        this.eventSource = eventSource;

        this.StartListeningForFolderChanges();
    }

    public void Dispose()
    {
        this.StopListeningForFolderChanges();
    }

    private void StartListeningForFolderChanges()
    {
        this.eventSource.FolderAdded += HandleFolderAdded;
    }

    private void HandleFolderAdded(object? sender, DatabaseFolder e)
    {
        // Special case empty list -- nothing to sort, so just add it
        if (this.targetList.Count == 0)
        {
            this.targetList.Add(e);
            return;
        }

        // Special case last item, and just add it
        if (this.comparer.Compare(e, this.targetList[this.targetList.Count - 1]) == 1)
        {
            this.targetList.Add(e);
            return;
        }

        // Loop through the list until we find a position that we're going to
        // insert at.
        for (var index = 0; index < this.targetList.Count; index += 1)
        {
            var item = this.targetList[index];
            var relativeOrder = this.comparer.Compare(e, item);

            // If existing item is equal to, or greater than the new one, we
            // haven't found a place to add it, so go around again
            if (relativeOrder > -1)
            {
                continue;
            }

            this.targetList.Insert(index, e);
            break;
        }
    }

    private void StopListeningForFolderChanges()
    {
        this.eventSource.FolderAdded -= HandleFolderAdded;
    }
}

