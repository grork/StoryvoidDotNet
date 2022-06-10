namespace Codevoid.Storyvoid;

/// <summary>
/// Manipulate article changes that have been (or should be) performed on the
/// database so that syncing can replay those changes at a later date
/// </summary>
public interface IArticleChangesDatabase
{
    /// <summary>
    /// Adds a URL w/ optional title to the pending database. It is not expected
    /// that items added here will be immediately visible in an app, because the
    /// actual article contents need to be round tripped via the instapaper
    /// service.
    /// </summary>
    /// <param name="uri">Url of the article to add</param>
    /// <param name="title">
    /// Optional; overrides (or sets) the title of the article compared to what
    /// the service would retrieve from the URL.
    /// </param>
    /// <returns>Pending Article Add information</returns>
    PendingArticleAdd CreatePendingArticleAdd(Uri uri, string? title);

    /// <summary>
    /// Given a URL retrieves the rest of the pending add (E.g. title)
    /// </summary>
    /// <param name="uri">Url to look up</param>
    /// <returns>Instance if found, null otherwise</returns>
    PendingArticleAdd? GetPendingArticleAddByUrl(Uri uri);

    /// <summary>
    /// Lists any pending article additions
    /// </summary>
    /// <returns>Unordered article additions currently present</returns>
    IList<PendingArticleAdd> ListPendingArticleAdds();

    /// <summary>
    /// Removes a pending article add that matches the supplied URL. If no url
    /// matches, no error is raised.
    /// </summary>
    /// <param name="url">Url for the pending add to remove</param>
    void RemovePendingArticleAdd(Uri url);

    /// <summary>
    /// Creates a pending article delete for the supplied ID
    /// </summary>
    /// <param name="articleId">Service ID of the article to be deleted</param>
    /// <returns>ID of the pending delete</returns>
    long CreatePendingArticleDelete(long articleId);

    /// <summary>
    /// Checks to see if there is a pending article delete for this ID
    /// </summary>
    /// <param name="articleId">Service ID of the article to check</param>
    bool HasPendingArticleDelete(long articleId);

    /// <summary>
    /// Lists any pending article deletes
    /// </summary>
    /// <returns>Unordered article deletes</returns>
    IList<long> ListPendingArticleDeletes();

    /// <summary>
    /// Removes a pending article delete that matches the supplied ID. If no id
    /// matches, no error is raised.
    /// </summary>
    void RemovePendingArticleDelete(long articleId);

    /// <summary>
    /// Creates a pending article state change (e.g. like/unlike) for the
    /// supplied ID.
    /// </summary>
    /// <param name="articleId">ID the state change is for</param>
    /// <param name="likeState">The state to apply (liked/unliked)</param>
    /// <returns>Pending article state change information</returns>
    PendingArticleStateChange CreatePendingArticleStateChange(long articleId, bool likeState);

    /// <summary>
    /// Given an article id, retrieves the pending article state change
    /// </summary>
    /// <param name="articleId">ID of the article to look up</param>
    /// <returns>Instance if found, null otherwise</returns>
    PendingArticleStateChange? GetPendingArticleStateChangeByArticleId(long articleId);

    /// <summary>
    /// Lists any pending article state changes (e.g. like/unlike)
    /// </summary>
    /// <returns>Unordered article state changes</returns>
    IList<PendingArticleStateChange> ListPendingArticleStateChanges();

    /// <summary>
    /// Removes a pending article state change that matches the supplied ID. If
    /// no id matches, no error is raised.
    /// </summary>
    /// <param name="articleId">ID of the pending state change to remove</param>
    void RemovePendingArticleStateChange(long articleId);
}