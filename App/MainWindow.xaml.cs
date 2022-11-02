using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Controls;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Codevoid.Storyvoid.App;

public sealed partial class MainWindow : Window
{
    private readonly IAccountSettings settings = new AccountSettings();
    public MainWindow()
    {
        this.InitializeComponent();

        if (settings.HasTokens)
        {
            this.SwitchToSignedIn();
            return;
        }

        this.SwitchToSignedOut();
    }

    private void AuthenticationControl_SuccessfullyAuthenticated(object? sender, Utilities.OAuth.ClientInformation e)
    {
        var authenticator = sender as AuthenticationControl;
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
        var authenticationControl = new AuthenticationControl();
        authenticationControl.SuccessfullyAuthenticated += AuthenticationControl_SuccessfullyAuthenticated;
        this.Content = authenticationControl;

        this.CleanupDB();
    }

    private IArticleDatabase? articleDatabase;
    private IFolderDatabase? folderDatabase;
    private IDbConnection? connection;

    [MemberNotNull(nameof(articleDatabase))]
    [MemberNotNull(nameof(folderDatabase))]
    private void OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open(); 
        connection.CreateDatabaseIfNeeded();

        this.connection = connection;

        this.articleDatabase = InstapaperDatabase.GetArticleDatabase(connection);
        this.folderDatabase = InstapaperDatabase.GetFolderDatabase(connection);
    }

    private void CleanupDB()
    {
        this.connection?.Dispose();
    }

    private void SwitchToSignedIn()
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

        var clearCredsButton = new Button()
        {
            Content = "Clear Credentials"
        };
        clearCredsButton.Click += ClearCredentials_Click;

        var buttons = new StackPanel()
        {
            Orientation = Orientation.Horizontal
        };

        buttons.Children.Add(clearCredsButton);

        // Open the database for use in the article list
        this.OpenDatabase();
        var articleList = new ArticleList(
            this.folderDatabase,
            this.articleDatabase,
            new DispatcherDatabaseEvents(this.DispatcherQueue),
            new ArticleListSettings()
        );

        var articleListControl = new ArticleListControl(articleList);

        var content = new StackPanel();
        content.Children.Add(label);
        content.Children.Add(buttons);
        content.Children.Add(articleListControl);

        this.Content = content;
    }
}
