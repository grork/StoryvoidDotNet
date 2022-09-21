using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Test.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleListTests : IDisposable
{
    private IFolderDatabase folderDatabase;
    private SqliteConnection connection;
    private ArticleList viewmodel;
    public ArticleListTests()
    {
        var (connection, folders, _, articles, _) = TestUtilities.GetDatabases();
        this.folderDatabase = folders;
        this.connection = connection;
        this.viewmodel = new ArticleList(this.folderDatabase);
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
    public void SettingCurrentFolderToSameValueDoesntRaisepRopertyChanged()
    {
        var propertyChangeRaised = false;
        this.viewmodel.PropertyChanged += (_, _) => propertyChangeRaised = true;

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
}