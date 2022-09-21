using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Test.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleListTests : IDisposable
{
    private (IFolderDatabase Folders, IArticleDatabase Articles) databases;
    private SqliteConnection connection;
    private ArticleList viewmodel;
    public ArticleListTests()
    {
        var (connection, folders, _, articles, _) = TestUtilities.GetDatabases();
        this.databases = (folders, articles);
        this.connection = connection;
        this.viewmodel = new ArticleList(this.databases.Folders, this.databases.Articles);
    }

    public void Dispose()
    {
        this.connection?.Close();
        this.connection?.Dispose();
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
}