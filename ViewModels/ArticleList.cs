﻿using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.ViewModels.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

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
public class ArticleList : INotifyPropertyChanged, IDisposable
{
    public readonly static string DefaultSortIdentifier = "OldestToNewest";
    public event PropertyChangedEventHandler? PropertyChanged;
    public readonly ICommand SyncCommand;
    public readonly ICommand LikeCommand;
    public readonly ICommand UnlikeCommand;

    private readonly IFolderDatabase folderDatabase;
    private readonly IArticleDatabase articleDatabase;
    private readonly IDatabaseEventSink eventSink;
    private DatabaseFolder currentFolder;
    private SortOption currentSort;
    private IArticleListSettings settings;
    private FolderListChangeProcessor folderChangeProcessor;
    private ArticleListChangeProcessor? articleListChangeProcessor;
    private ObservableCollection<DatabaseFolder> folders;
    private ObservableCollection<DatabaseArticle>? articles;

    /// <summary>
    /// Construct a new article list for the supplied databases.
    /// </summary>
    /// <param name="folderDatabase">Folder database</param>
    public ArticleList(
        IFolderDatabase folderDatabase,
        IArticleDatabase articleDatabase,
        IDatabaseEventSink eventSink,
        IArticleListSettings settings,
        SyncHelper syncHelper
    )
    {
        this.folderDatabase = folderDatabase;
        this.articleDatabase = articleDatabase;
        this.eventSink = eventSink;
        this.settings = settings;
        this.currentFolder = this.folderDatabase.GetFolderByLocalId(WellKnownLocalFolderIds.Unread)!;
        this.folders = new ObservableCollection<DatabaseFolder>(this.folderDatabase.ListAllFolders().OrderBy((f) => f, new FolderComparer()));

        this.folderChangeProcessor = new FolderListChangeProcessor(this.folders, this.eventSink);

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

        // Create the commands that will be used by the presentation layer
        this.SyncCommand = new SyncCommand(syncHelper);
        this.LikeCommand = new LikeCommand(articleDatabase);
        this.UnlikeCommand = new UnlikeCommand(articleDatabase);
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

    public void Dispose()
    {
        this.folderChangeProcessor.Dispose();
        this.articleListChangeProcessor?.Dispose();
        (this.SyncCommand as IDisposable)?.Dispose();
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
            this.RaisePropertyChanged();

            this.ResetArticleList();
        }
    }

    /// <summary>
    /// All folders currently in the database.
    /// </summary>
    public IReadOnlyCollection<DatabaseFolder> Folders => this.folders;

    /// <summary>
    /// Articles in the current folder, sorted by the current sort
    /// </summary>
    public IReadOnlyCollection<DatabaseArticle> Articles
    {
        get
        {
            if (this.articles is null)
            {
                Debug.Assert(this.articleListChangeProcessor is null);
                var articles = this.articleDatabase.ListArticlesForLocalFolder(this.CurrentFolder.LocalId).OrderBy((a) => a, this.CurrentSort.Comparer);
                this.articles = new ObservableCollection<DatabaseArticle>(articles);
                this.articleListChangeProcessor = new ArticleListChangeProcessor(this.articles, this.CurrentFolder.LocalId, this.eventSink, this.currentSort.Comparer);
            }

            return this.articles;
        }
    }

    private void ResetArticleList()
    {
        this.articleListChangeProcessor?.Dispose();
        this.articleListChangeProcessor = null;
        this.articles = null;
        this.RaisePropertyChanged(nameof(this.Articles));
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
            this.settings.SortIdentifier = value.Identifier;
            this.RaisePropertyChanged();
            this.ResetArticleList();
        }
    }
}