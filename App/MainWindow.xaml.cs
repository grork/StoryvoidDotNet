using Codevoid.Instapaper;
using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Controls;
using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using System.Data;
using System.Diagnostics.CodeAnalysis;

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

        this.SwitchToSignedOut();
    }

    private void AuthenticationControl_SuccessfullyAuthenticated(object? sender, ClientInformation e)
    {
        var authenticator = sender as Authenticator;
        authenticator!.SuccessfullyAuthenticated -= AuthenticationControl_SuccessfullyAuthenticated;
        this.SwitchToSignedIn();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        this.settings.ClearTokens();
        this.SwitchToSignedOut();
    }

    private void SwitchToSignedOut()
    {
        var accounts = new Accounts(this.settings.GetTokens()!);
        var authenticator = new Authenticator(accounts, this.settings);
        authenticator.SuccessfullyAuthenticated += AuthenticationControl_SuccessfullyAuthenticated;

        var authenticationControl = new AuthenticationControl(authenticator);
        this.DebugContent.Content = authenticationControl;

        this.CleanupDB();
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

    private void CleanupDB()
    {
        this.utilities.Dispose();
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

        var clearCredsButton = new Button() { Content = "Clear Credentials" };
        clearCredsButton.Click += ClearCredentials_Click;

        var performSyncButton = new Button() { Content = "Sync" };
        performSyncButton.Click += PerformSync_Click;

        var buttons = new StackPanel()
        {
            Orientation = Orientation.Horizontal
        };

        buttons.Children.Add(clearCredsButton);
        buttons.Children.Add(performSyncButton);

        // Open the database for use in the article list
        await this.OpenDatabase();
        var articleList = new ArticleList(
            this.folderDatabase!,
            this.articleDatabase!,
            this.dbEvents!,
            new ArticleListSettings()
        );

        var switchToArchive = new Button() { Content = "Archive" };
        switchToArchive.Click += (s, e) => articleList.CurrentFolder = articleList.Folders.First((f) => f.LocalId == WellKnownLocalFolderIds.Archive);

        var switchToHome = new Button() { Content = "Home" };
        switchToHome.Click += (s, e) => articleList.CurrentFolder = articleList.Folders.First((f) => f.LocalId == WellKnownLocalFolderIds.Unread);

        buttons.Children.Add(switchToArchive);
        buttons.Children.Add(switchToHome);


        var articleListControl = new ArticleListControl(articleList);

        var content = new StackPanel();
        content.Children.Add(label);
        content.Children.Add(buttons);
        content.Children.Add(articleListControl);

        this.DebugContent.Content = content;
    }

    private async void PerformSync_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        var tokens = this.settings.GetTokens()!;
        using var syncConnection = new SqliteConnection(this.utilities!.ConnectionString());
        syncConnection.Open();

        var folders = InstapaperDatabase.GetFolderDatabase(syncConnection, this.dbEvents);
        var folderChanges = InstapaperDatabase.GetFolderChangesDatabase(syncConnection);
        var articles = InstapaperDatabase.GetArticleDatabase(syncConnection, this.dbEvents);
        var articleChanges = InstapaperDatabase.GetArticleChangesDatabase(syncConnection);
        var foldersClient = new FoldersClient(tokens);
        var bookmarksClient = new BookmarksClient(tokens);

        var sync = new InstapaperSync(folders, folderChanges, foldersClient, articles, articleChanges, bookmarksClient);

        button.IsEnabled = false;
        try
        {
            await sync.SyncEverythingAsync();
        }
        finally
        {
            button.IsEnabled = true;
            syncConnection.Close();
        }
    }
}
