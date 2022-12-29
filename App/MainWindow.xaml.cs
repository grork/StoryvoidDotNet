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
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // When the main window is closed, we need to dispose the database to
        // give it a chance to do a full clean up & flush to disk. Without it,
        // it can recover, but it leaves the DB in a 'recovery' needed state
        this.utilities?.Dispose();
    }
}
