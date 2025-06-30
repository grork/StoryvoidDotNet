using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Pages;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace Codevoid.Storyvoid.App;

internal sealed partial class MainWindow : Window, IAppNavigation
{
    private readonly IAccountSettings settings = new AccountSettings();
    private readonly AppUtilities utilities;

    public MainWindow(Task<SqliteConnection> dbTask)
    {
        this.InitializeComponent();

        this.utilities = new AppUtilities(this, dbTask, this.DispatcherQueue);
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

    #region IAppNavigation Implementation
    /// <inheritdoc/>
    public void ClearStack()
    {
        this.MainThing.BackStack.Clear();
        this.MainThing.ForwardStack.Clear();
    }

    /// <inheritdoc/>
    public void ShowList(ArticleList articleList)
    {
        this.MainThing.Navigate(typeof(ArticleListPage), articleList);
    }

    /// <inheritdoc/>
    public void ShowLogin(Authenticator authenticator)
    {
        this.MainThing.Navigate(typeof(LoginPage), authenticator);
    }

    /// <inheritdoc/>
    public void ShowSigningOut()
    {
        this.MainThing.Navigate(typeof(SigningOutPage));
    }

    /// <inheritdoc/>
    public void ShowPlaceholder(NavigationParameter navigationParameter)
    {
        this.MainThing.Navigate(typeof(PlaceholderPage), navigationParameter);
    }
    #endregion
}
