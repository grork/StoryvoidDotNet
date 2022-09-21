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
    public void CanConstruct()
    {
        var articleList = new ArticleList(this.folderDatabase);
        Assert.NotNull(articleList);
    }
}