using Codevoid.Instapaper;
using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Controls;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;

namespace Codevoid.Storyvoid.App;

public sealed partial class MainWindow : Window
{
    private readonly IAccountSettings settings = new AccountSettings();
    private readonly AppUtilities utilities;
    public MainWindow(Task<SqliteConnection> dbTask)
    {
        this.InitializeComponent();
        this.utilities = new AppUtilities(this.MainThing, dbTask);
        this.utilities.ShowFirstPage();

        if (settings.HasTokens)
        {
            this.SwitchToSignedIn();
            return;
        }
    }

    private IArticleDatabase? articleDatabase;
    private IFolderDatabase? folderDatabase;
    private DispatcherDatabaseEvents? dbEvents;

    private async Task OpenDatabase()
    {
        var dataLayer = await this.utilities.GetDataLayer();
        this.dbEvents = dataLayer.Events;
        this.articleDatabase = dataLayer.Articles;
        this.folderDatabase = dataLayer.Folders;
    }

    private async void SwitchToSignedIn()
    {
        var label = new TextBlock()
        {
            Text = "Have Creds",
            FontSize = 72,
            FontWeight = FontWeights.ExtraLight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        // Open the database for use in the article list
        await this.OpenDatabase();
        var articleList = new ArticleList(
            this.folderDatabase!,
            this.articleDatabase!,
            this.dbEvents!,
            new ArticleListSettings()
        );

        var articleListControl = new ArticleListControl(articleList);

        var content = new StackPanel();
        content.Children.Add(label);
        content.Children.Add(articleListControl);

        this.DebugContent.Content = content;
    }
}
