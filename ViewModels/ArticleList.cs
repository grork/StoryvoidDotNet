using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Represents a primary view of articles for the given selected criteria of
/// viewing folder & applied sort.
/// </summary>
public class ArticleList : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly IFolderDatabase folderDatabase;
    private readonly IArticleDatabase articleDatabase;
    private DatabaseFolder _currentFolder;
    private IList<DatabaseArticle>? _articles;

    /// <summary>
    /// Construct a new article list for the supplied databases.
    /// </summary>
    /// <param name="folderDatabase">Folder database</param>
    public ArticleList(IFolderDatabase folderDatabase, IArticleDatabase articleDatabase)
    {
        this.folderDatabase = folderDatabase;
        this.articleDatabase = articleDatabase;
        this._currentFolder = this.folderDatabase.GetFolderByLocalId(WellKnownLocalFolderIds.Unread)!;
        this.Folders = new ObservableCollection<DatabaseFolder>(this.folderDatabase.ListAllFolders());
    }

    /// <summary>
    /// Raise PropertyChanged. By default (E.g. no parameter supplied), the
    /// callers member name (method, property) will be used as the property name
    /// </summary>
    /// <param name="propertyName">
    /// If provided, the name of the property to raise for. If not, derived from
    /// the immediate callers method/property name.
    /// </param>
    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        var handler = this.PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// The currently active folder (E.g., that which is being viewed). This is
    /// the folder for which articles will be returned.
    ///
    /// When set, the folder being viewed changes, and the bookmarks list is
    /// updated.
    /// </summary>
    public DatabaseFolder CurrentFolder
    {
        get => this._currentFolder;
        set
        {
            if (this._currentFolder == value)
            {
                return;
            }

            this._currentFolder = value;
            this._articles = null;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Articles));
        }
    }

    /// <summary>
    /// All folders currently in the database.
    /// </summary>
    public IList<DatabaseFolder> Folders { get; private set; }

    /// <summary>
    /// Articles in the current folder, sorted by the current sort
    /// </summary>
    public IList<DatabaseArticle> Articles
    {
        get
        {
            if (this._articles == null)
            {
                var articles = this.articleDatabase.ListArticlesForLocalFolder(this.CurrentFolder.LocalId);
                this._articles = new ObservableCollection<DatabaseArticle>(articles);
            }

            return this._articles;
        }
    }
}