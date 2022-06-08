namespace Codevoid.Storyvoid;

/// <summary>
/// Database store for Articles &amp; Folders from the Instapaper Service
/// </summary>
public interface IInstapaperDatabase : IDisposable
{
    /// <summary>
    /// Get the folder database for creating, listing, and removing folders
    /// </summary>
    IFolderDatabase FolderDatabase { get; }

    /// <summary>
    /// Get the article database for creating, listing, manipulating, and
    /// removing articles
    /// </summary>
    IArticleDatabase ArticleDatabase { get; }

    /// <summary>
    /// Get pending change database for creating, reading, and removing
    /// pending changes
    /// </summary>
    IChangesDatabase ChangesDatabase { get; }
}