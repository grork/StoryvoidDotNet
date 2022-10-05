namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Processes changes from an <see cref="IDatabaseEventSink"/> and applies them
/// to the provided list using <see cref="FolderComparer"/> to sort them
/// </summary>
public class FolderListChangeProcessor : BaseListChangeProcessor<DatabaseFolder>, IDisposable
{
    private readonly IDatabaseEventSink eventSource;

    public FolderListChangeProcessor(
        IList<DatabaseFolder> targetList,
        IDatabaseEventSink eventSource
    ) : base(targetList, new FolderComparer())
    {
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

    private void StopListeningForFolderChanges()
    {
        this.eventSource.FolderAdded -= HandleFolderAdded;
    }

    protected override bool IdentifiersMatch(DatabaseFolder first, DatabaseFolder second) => first.LocalId == second.LocalId;
    protected override bool IdentifiersMatch(DatabaseFolder item, long identifier) => item.LocalId == identifier;

    private void HandleFolderAdded(object? sender, DatabaseFolder e) => this.HandleItemAdded(e);
}