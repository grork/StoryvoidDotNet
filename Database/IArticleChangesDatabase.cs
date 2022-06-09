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
    /// Removes a pending article add that matches the supplied URL. If no url
    /// matches, no error is raised.
    /// </summary>
    /// <param name="url">Url for the pending add to remove</param>
    void RemovePendingArticleAdd(Uri url);
}