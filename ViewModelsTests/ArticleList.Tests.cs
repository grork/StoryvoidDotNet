using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using System.Data;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleListTests : IDisposable
{
    private (IFolderDatabase Folders, IArticleDatabase Articles) databases;
    private DatabaseEventClearingHouse clearingHouse = new DatabaseEventClearingHouse();
    private SqliteConnection connection;
    private ArticleList viewmodel;
    private MockArticleListSettings settings = new MockArticleListSettings();
    private SyncHelper syncHelper;

    public ArticleListTests()
    {
        var (connection, folders, _, articles, _) = TestUtilities.GetDatabases(this.clearingHouse);
        this.databases = (folders, articles);
        this.connection = connection;
        this.syncHelper = new SyncHelper(new MockArticleDownloader(),
            () => Task.FromResult<(IInstapaperSync, IDbConnection)>((new MockInstapaperSync(), connection)));

        this.viewmodel = new ArticleList(
            this.databases.Folders,
            this.databases.Articles,
            this.clearingHouse,
            this.settings,
            this.syncHelper
        );
    }

    public void Dispose()
    {
        this.viewmodel.Dispose();
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public void ConstructedListHasUnreadFolderAsDefault()
    {
        Assert.NotNull(this.viewmodel.CurrentFolder);
        Assert.Equal(WellKnownLocalFolderIds.Unread, this.viewmodel.CurrentFolder.LocalId);
    }

    [Fact]
    public void ConstructedListHasFoldersAvailable()
    {
        Assert.NotNull(this.viewmodel.Folders);
        Assert.NotEmpty(this.viewmodel.Folders);
    }

    [Fact]
    public void ConstructedHasContentsOfUnreadAvailableAfterConstruction()
    {
        Assert.NotNull(this.viewmodel.Articles);
        Assert.NotEmpty(this.viewmodel.Articles);

        var articles = this.databases.Articles.ListArticlesForLocalFolder(this.viewmodel.CurrentFolder.LocalId);
        Assert.Equal(articles, this.viewmodel.Articles);
    }

    [Fact]
    public void SettingCurrentFolderToSameValueDoesntRaisepRopertyChanged()
    {
        var propertyChangeRaised = false;
        this.viewmodel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(this.viewmodel.CurrentFolder))
            {
                return;
            }

            propertyChangeRaised = true;
        };

        this.viewmodel.CurrentFolder = this.viewmodel.CurrentFolder;
        Assert.False(propertyChangeRaised);
    }

    [Fact]
    public void SettingCurrentFolderToNewValueRaisesPropertyChanged()
    {
        var newFolder = (from folder in this.viewmodel.Folders
                         where folder.LocalId != WellKnownLocalFolderIds.Unread
                         select folder).First();

        Assert.PropertyChanged(this.viewmodel, nameof(this.viewmodel.CurrentFolder), () =>
        {
            this.viewmodel.CurrentFolder = newFolder;
        });
    }

    [Fact]
    public void SettingCurrentFolderRaisesArticlesPropertyChanged()
    {
        var newFolder = (from folder in this.viewmodel.Folders
                         where folder.LocalId != WellKnownLocalFolderIds.Unread
                         select folder).First();

        Assert.PropertyChanged(this.viewmodel, nameof(this.viewmodel.Articles), () =>
        {
            this.viewmodel.CurrentFolder = newFolder;
        });

        Assert.Equal(this.databases.Articles.ListArticlesForLocalFolder(newFolder.LocalId), this.viewmodel.Articles);
    }

    [Fact]
    public void DefaultSortIsInAvailableSortsList()
    {
        Assert.Contains(this.viewmodel.CurrentSort, this.viewmodel.Sorts);
    }

    [Fact]
    public void DefaultSortIsOldestToNewest()
    {
        Assert.Equal(ArticleList.DefaultSortIdentifier, this.viewmodel.CurrentSort.Identifier);
    }

    [Fact]
    public void SettingDifferentSortChangesTheResultsToMatchTheSelectedSort()
    {
        var beforeList = this.viewmodel.Articles;
        this.viewmodel.CurrentSort = this.viewmodel.Sorts.First((s) => s != this.viewmodel.CurrentSort);
        var afterList = this.viewmodel.Articles;

        Assert.NotEqual(beforeList, afterList);
    }

    [Fact]
    public void SettingDifferentSortThenBackToDefaultHasMatchingResults()
    {
        var beforeList = this.viewmodel.Articles;
        this.viewmodel.CurrentSort = this.viewmodel.Sorts.First((s) => s != this.viewmodel.CurrentSort);
        this.viewmodel.CurrentSort = this.viewmodel.Sorts.First();
        var afterList = this.viewmodel.Articles;

        Assert.Equal(beforeList, afterList);
    }

    [Fact]
    public void SettingCurrentSortToTheSameSortDoesNotRaisePropertyChangeNotificiations()
    {
        var articlesPropertyChangeRaised = false;
        var currentSortPropertyChangeRaised = false;
        this.viewmodel.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case "Articles":
                    articlesPropertyChangeRaised = true;
                    break;

                case "CurrentSort":
                    currentSortPropertyChangeRaised = true;
                    break;
            }
        };

        this.viewmodel.CurrentSort = this.viewmodel.CurrentSort;

        Assert.False(articlesPropertyChangeRaised, $"{nameof(this.viewmodel.Articles)} property change was raised");
        Assert.False(currentSortPropertyChangeRaised, $"{nameof(this.viewmodel.CurrentSort)} property change was raised");
    }

    [Fact]
    public void SettingCurrentSortToDifferentValueRaisesPropertyChangeNotificiations()
    {
        var articlesPropertyChangeRaised = false;
        var currentSortPropertyChangeRaised = false;
        this.viewmodel.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case "Articles":
                    articlesPropertyChangeRaised = true;
                    break;

                case "CurrentSort":
                    currentSortPropertyChangeRaised = true;
                    break;
            }
        };

        this.viewmodel.CurrentSort = this.viewmodel.Sorts.First((s) => s != this.viewmodel.CurrentSort);

        Assert.True(articlesPropertyChangeRaised, $"{nameof(this.viewmodel.Articles)} property change wasn't raised");
        Assert.True(currentSortPropertyChangeRaised, $"{nameof(this.viewmodel.CurrentSort)} property change wasn't raised");
    }

    [Fact]
    public void CurrentSortReflectsPersistedSettingOnConstruction()
    {
        var nonDefaultSortIdentifier = this.viewmodel.Sorts.First((s) => s.Identifier != ArticleList.DefaultSortIdentifier).Identifier;
        this.settings.SortIdentifier = nonDefaultSortIdentifier;

        using var alternativeViewModel = new ArticleList(
            this.databases.Folders,
            this.databases.Articles,
            this.clearingHouse,
            this.settings,
            this.syncHelper
        );

        Assert.Equal(nonDefaultSortIdentifier, alternativeViewModel.CurrentSort.Identifier);
    }

    [Fact]
    public void ChangingCurrentSortUpdatesSettingsToTheNewSort()
    {
        var nonDefaultSort = this.viewmodel.Sorts.First((s) => s.Identifier != ArticleList.DefaultSortIdentifier);
        this.viewmodel.CurrentSort = nonDefaultSort;

        Assert.Equal(nonDefaultSort.Identifier, settings.SortIdentifier);
    }

    [Fact]
    public void DatabaseChangesAreReflectedInTheFolderList()
    {
        var originalFolderCount = this.viewmodel.Folders.Count;

        var newFolder = this.databases.Folders.CreateFolder(nameof(DatabaseChangesAreReflectedInTheFolderList));

        Assert.Equal(originalFolderCount + 1, this.viewmodel.Folders.Count);
        Assert.Equal(newFolder, this.viewmodel.Folders.Last());
    }

    [Fact]
    public void DatabaseChangesAreReflectedInTheArticleList()
    {
        var originalArticleCount = this.viewmodel.Articles.Count;

        var newArticle = this.databases.Articles.AddArticleToFolder(TestUtilities.GetRandomArticle(), this.viewmodel.CurrentFolder.LocalId);

        Assert.Equal(originalArticleCount + 1, this.viewmodel.Articles.Count);
        Assert.Contains(newArticle, this.viewmodel.Articles);
    }

    [Fact]
    public void DatabaseChangesAreOnlyReflectedInTheNewListWhenChangingSorts()
    {
        var originalList = this.viewmodel.Articles;
        var originalArticleCount = this.viewmodel.Articles.Count;

        this.viewmodel.CurrentSort = this.viewmodel.Sorts.First((s) => s != this.viewmodel.CurrentSort);

        var newList = this.viewmodel.Articles;

        var newArticle = this.databases.Articles.AddArticleToFolder(TestUtilities.GetRandomArticle(), this.viewmodel.CurrentFolder.LocalId);

        Assert.Equal(originalArticleCount, originalList.Count);
        Assert.DoesNotContain(newArticle, originalList);

        Assert.Equal(originalArticleCount + 1, this.viewmodel.Articles.Count);
        Assert.Contains(newArticle, this.viewmodel.Articles);
    }
}