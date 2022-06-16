using System.Data;

namespace Codevoid.Storyvoid;

/// <summary>
/// Article sourced from the Database
/// </summary>
public sealed record DatabaseArticle
{
    private DatabaseArticle() { }

    /// <summary>
    /// Local-only information for this article, if present.
    /// </summary>
    public DatabaseLocalOnlyArticleState? LocalOnlyState { get; init; }

    /// <summary>
    /// Convenience access to check if have local-only state information
    /// available.
    /// </summary>
    public bool HasLocalState => this.LocalOnlyState != null;

    /// <summary>
    /// Article ID in the local database, and on the service
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// URL that this article represents
    /// </summary>
    public Uri Url { get; init; } = new Uri("unset://unset");

    /// <summary>
    /// Display title for this article
    /// </summary>
    public string Title { get; init; } = String.Empty;

    /// <summary>
    /// Optional description of the article
    /// </summary>
    public string Description { get; init; } = String.Empty;

    /// <summary>
    /// Current read progress of the article -- between 0.0 and 1.0
    /// </summary>
    public float ReadProgress { get; init; }

    /// <summary>
    /// Time that the progress was last changed
    /// </summary>
    public DateTime ReadProgressTimestamp { get; init; }

    /// <summary>
    /// Hash provided by the service of the article reading progress &amp;
    /// change timestamp.
    /// </summary>
    public string Hash { get; init; } = String.Empty;

    /// <summary>
    /// Has this article been liked
    /// </summary>
    public bool Liked { get; init; }

    /// <summary>
    /// Converts a raw database row into a hydrated article instance
    /// </summary>
    /// <param name="row">Row to read article data from</param>
    /// <returns>Instance of the article object for this row</returns>
    internal static DatabaseArticle FromRow(IDataReader row)
    {
        var id = row.GetInt64("id");
        var url = row.GetUri("url");
        var title = row.GetString("title");
        var progress = row.GetFloat("read_progress");
        var progressTimestamp = row.GetDateTime("read_progress_timestamp");
        var hash = row.GetString("hash");
        var liked = row.GetBoolean("liked");
        var description = String.Empty;
        DatabaseLocalOnlyArticleState? localOnlyState = null;

        if (!row.IsDBNull("description"))
        {
            description = row.GetString("description");
        }

        // If there is an associated article ID, this implies that there is
        // download / local state.
        if (row.HasColumn("article_id") && !row.IsDBNull("article_id"))
        {
            localOnlyState = DatabaseLocalOnlyArticleState.FromRow(row);
        }

        var article = new DatabaseArticle()
        {
            Id = id,
            Url = url,
            Title = title,
            ReadProgress = progress,
            ReadProgressTimestamp = progressTimestamp,
            Hash = hash,
            Liked = liked,
            Description = description,
            LocalOnlyState = localOnlyState
        };

        return article;
    }
}