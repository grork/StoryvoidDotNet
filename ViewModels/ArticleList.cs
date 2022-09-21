namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Represents a primary view of articles for the given selected criteria of
/// viewing folder & applied sort.
/// </summary>
public class ArticleList
{
    private readonly IFolderDatabase folderDatabase;

    /// <summary>
    /// Construct a new article list for the supplied databases.
    /// </summary>
    /// <param name="folderDatabase">Folder database</param>
    public ArticleList(IFolderDatabase folderDatabase)
    {
        this.folderDatabase = folderDatabase;
    }
}