using System.Data;

namespace Codevoid.Storyvoid;

/// <summary>
/// Pending Folder Add sourced from the Database
/// </summary>
public sealed record PendingFolderAdd
{
    private PendingFolderAdd() { }

    /// <summary>
    /// ID of the folder that has been added, so additional information may
    /// be later retrieved about that folder
    /// </summary>
    public long FolderLocalId { get; init; }

    /// <summary>
    /// Title of the folder that has been added
    /// </summary>
    public string Title { get; init; } = String.Empty;

    /// <summary>
    /// Converts a raw database row into a hydrated Pending Folder Add
    /// instance
    /// </summary>
    /// <param name="row">Row to read Pending Folder Add data from</param>
    /// <returns>Instance of a Pending Folder Add</returns>
    internal static PendingFolderAdd FromRow(IDataReader row)
    {
        var folderLocalId = row.GetInt64("local_id");
        var title = row.GetString("title")!;

        var change = new PendingFolderAdd()
        {
            FolderLocalId = folderLocalId,
            Title = title
        };

        return change;
    }
}

/// <summary>
/// Pending Folder Delete sourced from the database
/// </summary>
public sealed record PendingFolderDelete
{
    private PendingFolderDelete() { }

    /// <summary>
    /// Service ID for the folder that is being deleted.
    /// </summary>
    public long ServiceId { get; init; }

    /// <summary>
    /// Title of the folder that was deleted, so that if the folder is
    /// readded locally we can resurrect the folder w/ the same service ID
    /// </summary>
    public string Title { get; init; } = String.Empty;

    /// <summary>
    /// Converts a raw database row into a hydrated Pending Folder Delete
    /// </summary>
    /// <param name="row">Row to read Pending Folder Delete from</param>
    /// <returns></returns>
    internal static PendingFolderDelete FromRow(IDataReader row)
    {
        var serviceId = row.GetInt64("service_id");
        var title = row.GetString("title")!;

        var change = new PendingFolderDelete()
        {
            ServiceId = serviceId,
            Title = title
        };

        return change;
    }
}

/// <summary>
/// Pending Article Add sourced from the database
/// </summary>
public sealed record PendingArticleAdd
{
    private PendingArticleAdd() { }

    /// <summary>
    /// URL that this article for. Must be unique within the database.
    /// </summary>
    public Uri Url { get; init; } = new Uri("unset://unset");

    /// <summary>
    /// Optional title that will override any service-derived title when synced
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// For a database row, convert it to a complete instance of a pending
    /// article add
    /// </summary>
    /// <param name="row">Row to convert</param>
    /// <returns>Instance of a pending article add</returns>
    internal static PendingArticleAdd FromRow(IDataReader row)
    {
        var urlString = row.GetString("url");
        var url = new Uri(urlString);
        String? title = null;

        if(!row.IsDBNull("title"))
        {
            title = row.GetString("title");
        }

        return new ()
        {
            Url = url,
            Title = title
        };
    }
}

/// <summary>
/// Pending Article State Change sourced from the database
/// </summary>
public sealed class PendingArticleStateChange
{
    private PendingArticleStateChange() { }

    /// <summary>
    /// Article ID that this state change is for
    /// </summary>
    public long ArticleId { get; init; }

    /// <summary>
    /// New like state for the article
    /// </summary>
    public bool Liked { get; init; }

    /// <summary>
    /// For a database row, convert it to a complete instance of a pending
    /// article state change.
    /// </summary>
    /// <param name="row">Row to convert</param>
    /// <returns>Instance of a pending article state change</returns>
    internal static PendingArticleStateChange FromRow(IDataReader row)
    {
        var articleId = row.GetInt64("article_id");
        var likeState = row.GetBoolean("liked");

        return new PendingArticleStateChange()
        {
            ArticleId = articleId,
            Liked = likeState
        };
    }
}

/// <summary>
/// Pending Article Move sourced from the database
/// </summary>
public sealed class PendingArticleMove
{
    private PendingArticleMove() { }

    /// <summary>
    /// Article ID that this move is for
    /// </summary>
    public long ArticleId { get; init; }

    /// <summary>
    /// Desintation local ID for the article
    /// </summary>
    public long DestinationFolderLocalId { get; init; }

    /// <summary>
    /// For a database row, convert it to a complete instance of a pending
    /// article move
    /// </summary>
    /// <param name="row">Row to convert</param>
    /// <returns>Instance of a pending article move</returns>
    internal static PendingArticleMove FromRow(IDataReader row)
    {
        var articleId = row.GetInt64("article_id");
        var destinationFolderLocalId = row.GetInt64("destination_local_id");

        return new PendingArticleMove()
        {
            ArticleId = articleId,
            DestinationFolderLocalId = destinationFolderLocalId
        };
    }
}