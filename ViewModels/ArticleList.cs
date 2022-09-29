using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Read &amp; write Article List settings, e.g. the current sort
/// </summary>
public interface IArticleListSettings
{
    /// <summary>
    /// Stable identifier for the sort that is persisted across sessions to
    /// maintain the selected sort in the article list.
    /// </summary>
    string SortIdentifier { get; set; }
}

/// <summary>
/// Contains the user visible label for the sort option, as well as the comparer
/// that implements that sort.
/// </summary>
/// <param name="Label">User-visible label for the sort</param>
/// <param name="Comparer">Instance of the comparer for this sort</param>
public record SortOption(
    string Label,
    IComparer<DatabaseArticle> Comparer,
    string Identifier
);

/// <summary>
/// Represents a primary view of articles for the given selected criteria of
/// viewing folder & applied sort.
/// </summary>
public class ArticleList : INotifyPropertyChanged
{
    public readonly static string DefaultSortIdentifier = "OldestToNewest";
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly IFolderDatabase folderDatabase;
    private readonly IArticleDatabase articleDatabase;
    private DatabaseFolder currentFolder;
    private SortOption currentSort;
    private IArticleListSettings settings;
    private IList<DatabaseArticle>? articles;

    /// <summary>
    /// Construct a new article list for the supplied databases.
    /// </summary>
    /// <param name="folderDatabase">Folder database</param>
    public ArticleList(
        IFolderDatabase folderDatabase,
        IArticleDatabase articleDatabase,
        IArticleListSettings settings
    )
    {
        this.folderDatabase = folderDatabase;
        this.articleDatabase = articleDatabase;
        this.settings = settings;
        this.currentFolder = this.folderDatabase.GetFolderByLocalId(WellKnownLocalFolderIds.Unread)!;
        this.Folders = new ObservableCollection<DatabaseFolder>(this.folderDatabase.ListAllFolders());

        this.Sorts = new List<SortOption>
        {
            new SortOption(
                "Oldest First",
                new OldestToNewestArticleComparer(),
                DefaultSortIdentifier
            ),
            new SortOption(
                "Newest First",
                new NewestToOldestArticleComparer(),
                "NewestToOldest"
            ),
            new SortOption(
                "By Progress",
                new ByProgressDescendingComparer(),
                "ByProgress"
            )
        };

        // Set the default sort to match that supplied from settings.
        this.currentSort = this.Sorts.First((s) => s.Identifier == settings.SortIdentifier);
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
        get => this.currentFolder;
        set
        {
            if (this.currentFolder == value)
            {
                return;
            }

            this.currentFolder = value;
            this.articles = null;
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
            if (this.articles == null)
            {
                var articles = this.articleDatabase.ListArticlesForLocalFolder(this.CurrentFolder.LocalId).OrderBy((a) => a, this.CurrentSort.Comparer);
                this.articles = new ObservableCollection<DatabaseArticle>(articles);
            }

            return this.articles;
        }
    }

    /// <summary>
    /// Available sort options
    /// </summary>
    public IReadOnlyCollection<SortOption> Sorts { get; private set; }

    /// <summary>
    /// Currently applied sort. Setting this to a new value will:
    /// - Refresh the list of articles, sorted by whatever this sort is
    /// - Assign the sort to settings for storage and later restoration
    /// </summary>
    public SortOption CurrentSort
    {
        get { return this.currentSort; }
        set
        {
            if (this.currentSort == value)
            {
                return;
            }

            Debug.Assert(this.Sorts.Contains(value));

            this.currentSort = value;
            this.articles = null;
            this.settings.SortIdentifier = value.Identifier;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged("Articles");
        }
    }
}