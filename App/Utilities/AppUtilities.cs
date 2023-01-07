using Codevoid.Instapaper;
using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Pages;
using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Container type for passing parameters to pages where they all need an
/// <see cref="IAppUtilities"/> instance
/// </summary>
/// <param name="Parameter">Page-implementation specific parameter</param>
/// <param name="Utilities">Utilities instance</param>
internal record NavigationParameter(object? Parameter, IAppUtilities Utilities);

/// <summary>
/// Utilities that the majority of pages / components will need. Intended to
/// hide the complexity of the implementation classes from consumers.
/// </summary>
interface IAppUtilities
{
    /// <summary>
    /// Show the login page, allowing someone to enter user + password
    /// </summary>
    void ShowLogin();

    /// <summary>
    /// Shows the Article List
    /// </summary>
    void ShowList();

    /// <summary>
    /// Shows a placeholder page; the parameter is displayed as the result of a
    /// call to `ToString()` on the parameter instance.
    /// </summary>
    /// <param name="parameter">Optional parameter</param>
    void ShowPlaceholder(object? parameter = null);

    /// <summary>
    /// Clears the credentials, local database + files, and displays the login
    /// page once complete.
    /// </summary>
    void Signout();
}

/// <summary>
/// Utility Class to allow navigations to be decoupled from the views.
/// </summary>
internal sealed class AppUtilities : IAppUtilities, IDisposable
{
    internal record DataLayer(
        IArticleDatabase Articles,
        IFolderDatabase Folders,
        DispatcherDatabaseEvents DatabaseEvents,
        IDisposable Ledger,
        SyncHelper SyncHelper,
        DispatcherSyncEvents SyncEvents,
        ArticleDownloader ArticleDownloader,
        ArticleDownloaderEvents DownloderEvents,
        SqliteConnection Connection
    );

    /// <summary>
    /// Count to help differentiate different non-parameterized placeholder
    /// pages
    /// </summary>
    private static int placeholderCount = 1;

    /// <summary>
    /// Default filename for the database stored in the local file system
    /// </summary>
    private static readonly string DATABASE_FILE_NAME = "storyvoid";

    /// <summary>
    /// Default folder for the downloaded articles in the local file system
    /// </summary>
    private static readonly string ARTICLE_DOWNLOADS_FOLDER = "articles";

    private bool disposed = false;
    private Frame frame;
    private IAccountSettings accountSettings = new AccountSettings();
    private DataLayer? dataLayer;
    private Task<SqliteConnection>? dbTask;
    private IList<string> OperationLog = new ObservableCollection<string>();

    internal AppUtilities(Frame frame, Task<SqliteConnection> connection)
    {
        this.dbTask = connection;
        this.frame = frame;
    }

    /// <inheritdoc />
    public void ShowLogin()
    {
        var authenticator = new Authenticator(new Accounts(this.accountSettings.GetTokens()), this.accountSettings);

        authenticator.SuccessfullyAuthenticated += (sender, clientInformation) =>
        {
            this.ShowList();
            this.frame.BackStack.Clear();
        };
        this.frame.Navigate(typeof(LoginPage), authenticator);
    }

    /// <inheritdoc/>
    public async void ShowList()
    {
        var dataLayer = await this.GetDataLayer();
        var articleList = new ArticleList(
            dataLayer.Folders,
            dataLayer.Articles,
            dataLayer.DatabaseEvents,
            new ArticleListSettings()
        );

        this.frame.Navigate(typeof(ArticleListPage), articleList);
    }

    /// <summary>
    /// Show the signing out page
    /// </summary>
    public void ShowSigningOut()
    {
        this.frame.Navigate(typeof(SigningOutPage));
    }

    /// <inheritdoc/>
    public void ShowPlaceholder(object? parameter = null)
    {
        async Task<(IFolderDatabase, SyncHelper)> LocalWork()
        {
            var dataLayer = await this.GetDataLayer();
            return (dataLayer.Folders, dataLayer.SyncHelper);
        }

        var placeholderParameter = new PlaceholderParameter(parameter ?? placeholderCount++, LocalWork(), this.OperationLog);

        var navigationParameter = new NavigationParameter(placeholderParameter, this);
        this.frame.Navigate(typeof(PlaceholderPage), navigationParameter);
    }

    /// <summary>
    /// Inspects state and shows the required first page. E.g. If we have creds
    /// then we'll show the article list, otherwise the login page.
    /// 
    /// This is intentionally not part of the interface, because it's only
    /// needed early in app startup.
    /// </summary>
    public void ShowFirstPage()
    {
        if (!this.accountSettings.HasTokens)
        {
            this.ShowLogin();
            return;
        }

        this.ShowList();
    }

    /// <inheritdoc/>
    public async void Signout()
    {
        // Immediately navigate to the signing out page, and clear any other
        // pages.
        this.ShowSigningOut();
        this.frame.BackStack.Clear();
        this.frame.ForwardStack.Clear();

        // Clear any credentials
        this.accountSettings.ClearTokens();

        // Clean up the database in the background, 
        var cleanUpTask = Task.Run(() =>
        {
            this.CloseDatabase();
            AppUtilities.DeleteLocalFiles();
        });

        // Make sure we show the signing out page for a minimum time
        await Task.WhenAll(cleanUpTask, Task.Delay(1000));

        // Initiate the new, empty, database
        this.dbTask = Task.Run(AppUtilities.OpenDatabaseAsync);

        // Navigate to our default page
        this.ShowFirstPage();
    }

    /// <summary>
    /// Lock for making sure we rendezvous multiple calls to getting the
    /// layer if it's not been created yet.
    /// </summary>
    private object dataLayerLock = new object();
    private Task<DataLayer>? dataLayerTask;

    internal Task<DataLayer> GetDataLayer()
    {
        async Task<DataLayer> GetDataLayerWork()
        {
            // Get the DB Connection
            Debug.Assert(dbTask is not null);
            var dbConnection = await dbTask;
            this.dbTask?.Dispose();
            this.dbTask = null;

            // Get inputs for the data layer
            var connectionStringForSync = dbConnection.ConnectionString;
            var clientInformation = this.accountSettings.GetTokens();

            // Create our data layer
            var databaseEvents = new DispatcherDatabaseEvents(this.frame.DispatcherQueue);
            var articleDB = InstapaperDatabase.GetArticleDatabase(dbConnection, databaseEvents);
            var folderDB = InstapaperDatabase.GetFolderDatabase(dbConnection, databaseEvents);
            var ledger = InstapaperDatabase.GetLedger(folderDB, articleDB);
            var downloaderEvents = new ArticleDownloaderEvents(this.frame.DispatcherQueue);
            var articleDownloader = new ArticleDownloader(
                AppUtilities.LocalArticlesFolderPath,
                articleDB,
                new BookmarksClient(clientInformation),
                clientInformation,
                downloaderEvents
            );
            var syncEvents = new DispatcherSyncEvents(this.frame.DispatcherQueue);
            var syncHelper = new SyncHelper(articleDownloader, async () =>
            {
                // Syncing should happen against a separate DB connection to
                // keep the transactions isolated & prevent dirty-reads. We also
                // want to open the DB on a separate thread so that any disk
                // delays don't hold up the caller.
                //
                // Note, per the contract of sync helper, it will be responsible
                // for closing the database when it's done.
                var syncDb = await Task.Run(() =>
                {
                    var syncConnection = new SqliteConnection(connectionStringForSync);
                    syncConnection.Open();

                    return syncConnection;
                });

                // Create the sync instance itself
                var folders = InstapaperDatabase.GetFolderDatabase(syncDb, databaseEvents);
                var folderChanges = InstapaperDatabase.GetFolderChangesDatabase(syncDb);
                var articles = InstapaperDatabase.GetArticleDatabase(syncDb, databaseEvents);
                var articleChanges = InstapaperDatabase.GetArticleChangesDatabase(syncDb);
                var foldersClient = new FoldersClient(clientInformation);
                var bookmarksClient = new BookmarksClient(clientInformation);

                var sync = new InstapaperSync(
                    folders,
                    folderChanges,
                    foldersClient,
                    articles,
                    articleChanges,
                    bookmarksClient,
                    syncEvents
                );

                return (sync, syncDb);
            })
            { DownloadArticles = true }; // Remove when we start downloading articles

            var databases = new DataLayer(
                articleDB,
                folderDB,
                databaseEvents,
                ledger,
                syncHelper,
                syncEvents,
                articleDownloader,
                downloaderEvents,
                dbConnection
            );

            // Log some of the events in sync, database, download to the
            // operation log so it's easy to see whats going on
            syncEvents.SyncStarted += (o, a) => this.OperationLog.Add("Sync Started");
            syncEvents.SyncEnded += (o, a) => this.OperationLog.Add("Sync Ended");
            downloaderEvents.DownloadingStarted += (o, a) => this.OperationLog.Add("Article Download Started");
            downloaderEvents.DownloadingCompleted += (o, a) => this.OperationLog.Add("Article Download Completed");

            lock (this.dataLayerLock)
            {
                Debug.Assert(this.dataLayer is null);
                this.dataLayer = databases;
            }

            return databases;
        }

        // Opportunistically check if we've got the datalayer. If we have, we
        // don't need to perform any operations, we can just return the result.
        var localDataLayer = this.dataLayer;
        Task<DataLayer>? localDataLayerTask = null;
        if (localDataLayer is not null)
        {
            return Task.FromResult(localDataLayer);
        }

        // But we didn't have it, so we're going to take a lock. We will check
        // again to ensure a concucrent operation did not complete while
        // acquiring the lock.
        lock (this.dataLayerLock)
        {
            localDataLayer = this.dataLayer;
            if (localDataLayer is not null)
            {
                return Task.FromResult(localDataLayer);
            }

            // No layer, lets kick off an operation to complete
            localDataLayerTask = this.dataLayerTask;
            if (localDataLayerTask is null)
            {
                localDataLayerTask = this.dataLayerTask = GetDataLayerWork();
            }
        }

        return localDataLayerTask!;
    }

    /// <summary>
    /// TEMPORARY helper as part of the transition to real navigation / sync
    /// </summary>
    internal string ConnectionString()
    {
        return this.dataLayer!.Connection.ConnectionString;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.CloseDatabase();
        this.disposed = true;
    }

    private void CloseDatabase()
    {
        // Yes, I know this will block. Yes, thats the point.
        var localDataLayer = this.dataLayerTask?.Result;
        this.dataLayerTask?.Dispose();

        if (localDataLayer is not null)
        {
            localDataLayer.Connection.Close();
            localDataLayer.Connection.Dispose();
        }

        // See: https://github.com/dotnet/efcore/issues/26580#issuecomment-963938116
        SqliteConnection.ClearAllPools();

        this.dataLayer = null;
    }

    /// <summary>
    /// Get the folder where we store local data. This is expected to be
    /// persistent and saved across updates
    /// </summary>
    private static string LocalDataFolderPath => ApplicationData.Current.LocalCacheFolder.Path;

    /// <summary>
    /// Gets the folder where downloaded articles are stored. This is expected
    /// to be persistent and saved across updates
    /// </summary>
    private static string LocalArticlesFolderPath => Path.Combine(AppUtilities.LocalDataFolderPath, ARTICLE_DOWNLOADS_FOLDER);

    /// <summary>
    /// Deletes the files that make up the local database from the data folder.
    /// 
    /// Ensure you've closed all connections to the database, or this will fail
    /// </summary>
    internal static void DeleteLocalFiles()
    {
        var localDataPath = AppUtilities.LocalDataFolderPath;

        // Use the database filename stub to find all the files that
        // are part of the SQLite database, so all the DB state is
        // deleted.
        foreach (var dbFile in Directory.GetFiles(localDataPath, $"{DATABASE_FILE_NAME}.*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(dbFile);
        }

        try
        {
            Directory.Delete(AppUtilities.LocalArticlesFolderPath, true);
        }
        catch { };
    }

    /// <summary>
    /// Opens or creates a local database file. If the database is present, it
    /// will be opened. If it's not present, it will be created and initialized
    /// with the default tables etc.
    /// </summary>
    /// <returns>Connection to the database</returns>
    static internal SqliteConnection OpenDatabaseAsync()
    {
        var localDataPath = AppUtilities.LocalDataFolderPath;
        var databaseFile = Path.Combine(localDataPath, $"{DATABASE_FILE_NAME}.db");
        var connectionString = $"Data Source={databaseFile}";

#if DEBUG
        // Enable external quick-and-simple switch to using an in memory
        // database, or deletion of the existing database file & any
        // state that it might have.
        var useInMemoryDatabase = KeyStateChecker.IsKeyPressed(KeyStateChecker.Keys.VK_SHIFT);
        var deleteLocalDatabaseFirst = KeyStateChecker.IsKeyPressed(KeyStateChecker.Keys.VK_ALT);
        if (useInMemoryDatabase)
        {
            connectionString = "Data Source=StaysInMemory;Mode=Memory;Cache=Shared";
        }

        if (deleteLocalDatabaseFirst)
        {
            AppUtilities.DeleteLocalFiles();
        }
#endif

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        connection.CreateDatabaseIfNeeded();

        return connection;
    }

#if DEBUG
    /// <summary>
    /// Simple checker for keys being pressed. Intended to only be used during
    /// app launch for debugging purposes.
    /// 
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeystate
    /// for more details.
    /// </summary>
    private static class KeyStateChecker
    {
        public enum Keys
        {
            VK_SHIFT = 0x10,
            VK_ALT = 0x12
        }

        private const int KEY_PRESSED = 0x8000;

        [DllImport("USER32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public static bool IsKeyPressed(Keys keyToCheckForBeingPressed)
        {
            var state = GetKeyState((int)keyToCheckForBeingPressed);
            return ((state & 0x8000) != 0);
        }
    }
#endif
}
