using Microsoft.Data.Sqlite;
using Microsoft.UI.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Codevoid.Storyvoid.App;
using Strings = Codevoid.Storyvoid.Resources;

/// <summary>
/// Main entrypoint into the application that handles deciding what initial UI
/// to handle, and any non-simple-app-launch handling.
/// </summary>
public partial class Launcher : Application
{
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

    private static readonly string DATABASE_FILE_NAME = "storyvoid";

    public Launcher()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // We can't run without API Keys, and people won't read the readme, so
        // check if we've got placeholder API Keys, and show the appropriate
        // message if we do. Normal app startup shouldn't happen in this
        // scenario.
        Window? mainWindow = null;
#pragma warning disable CS0162
        if (InstapaperAPIKey.CONSUMER_KEY_SECRET == "PLACEHOLDER"
         || InstapaperAPIKey.CONSUMER_KEY == "PLACEHOLDER")
        {
            mainWindow = this.GetNoApiKeysWindow();
        }
        else
        {
            // Initiate DB opening on a separate thread, so that by the time we
            // actually want it, it shuld be complete.
            var dbTask = Task.Run(() =>
            {
                var localCacheFolder = ApplicationData.Current.LocalCacheFolder;
                var databaseFile = Path.Combine(localCacheFolder.Path, $"{DATABASE_FILE_NAME}.db");
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

                if(deleteLocalDatabaseFirst)
                {
                    // Use the database filename stub to find all the files that
                    // are part of the SQLite database, so all the DB state is
                    // deleted.
                    foreach (var dbFile in Directory.GetFiles(localCacheFolder.Path, $"{DATABASE_FILE_NAME}.*", SearchOption.TopDirectoryOnly))
                    {
                        File.Delete(dbFile);
                    }
                }
#endif

                var connection = new SqliteConnection(connectionString);
                connection.Open();
                connection.CreateDatabaseIfNeeded();

                return connection;
            });

            mainWindow = new MainWindow(dbTask);
#pragma warning restore
        }

        // Source the window title from the manifest, so we have a single source
        mainWindow.Title = AppInfo.Current.DisplayInfo.DisplayName;
        mainWindow.Activate();
    }

    private Window GetNoApiKeysWindow()
    {
        var errorMessage = Strings.Errors.NoApiKeysSet;
        Debug.Fail(errorMessage); // Force break into the debugger

        return new Window()
        {
            Content = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 72,
                FontWeight = FontWeights.ExtraLight,
                Text = errorMessage
            }
        };
    }
}
