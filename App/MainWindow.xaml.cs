using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Controls;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;

namespace Codevoid.Storyvoid.App;

public sealed partial class MainWindow : Window
{
    private readonly IAccountSettings settings = new AccountSettings();
    private readonly AppUtilities utilities;
    private SystemBackdropHelper backdropHelper;

    public MainWindow(Task<SqliteConnection> dbTask)
    {
        this.InitializeComponent();

        this.backdropHelper = new SystemBackdropHelper(this, this.MainThing);
        this.utilities = new AppUtilities(this.MainThing, dbTask);
        this.Closed += MainWindow_Closed;

#if DEBUG
        // We want to make it easy -- at least in debug mode -- to be able to
        // get to the placeholder page 'cause a) it's useful to nav b) it has
        // utility buttons on it.
        var placeholderShortcut = new KeyboardAccelerator()
        {
            Modifiers = Windows.System.VirtualKeyModifiers.Control,
            Key = Windows.System.VirtualKey.P
        };
        placeholderShortcut.Invoked += (s, a) => this.utilities.ShowPlaceholder();
        this.MainThing.KeyboardAccelerators.Add(placeholderShortcut);
#endif

        this.utilities.ShowFirstPage();

        if (settings.HasTokens)
        {
            this.SwitchToSignedIn();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // When the main window is closed, we need to dispose the database to
        // give it a chance to do a full clean up & flush to disk. Without it,
        // it can recover, but it leaves the DB in a 'recovery' needed state
        this.utilities?.Dispose();
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
