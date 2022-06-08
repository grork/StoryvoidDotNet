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
    /// <param name="uri">Url of the bookmark to add</param>
    /// <param name="title">
    /// Optional; overrides (or sets) the title of the article compared to what
    /// the service would retrieve from the URL.
    /// </param>
    /// <returns>ID of the pending article addition</returns>
    long CreatePendingArticleAdd(Uri uri, string? title);
}