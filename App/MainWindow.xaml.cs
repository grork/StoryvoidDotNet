using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

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
        this.MainThing.KeyDown += (s, a) =>
        {
            if (!(a.Key == VirtualKey.P && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) == CoreVirtualKeyStates.Down))
            {
                return;
            }

            this.utilities.ShowPlaceholder();
        };
#endif

        this.utilities.ShowFirstPage();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // When the main window is closed, we need to dispose the database to
        // give it a chance to do a full clean up & flush to disk. Without it,
        // it can recover, but it leaves the DB in a 'recovery' needed state
        this.utilities?.Dispose();
    }
}
