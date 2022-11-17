using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Pages;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Codevoid.Storyvoid.Utilities;

internal record NavigationParameter(object? Parameter, IAppUtilities Utilities);

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
}

/// <summary>
/// Utility Class to allow navigations to be decoupled from the views.
/// </summary>
internal sealed class AppUtilities : IAppUtilities, IDisposable
{
    internal record DataLayer(
        IArticleDatabase Articles,
        IFolderDatabase Folders,
        DispatcherDatabaseEvents Events,
        SqliteConnection Connection
    );

    /// <summary>
    /// Count to help differentiate different non-parameterized placeholder
    /// pages
    /// </summary>
    private static int placeholderCount = 1;

    private bool disposed = false;
    private Frame frame;
    private IAccountSettings accountSettings = new AccountSettings();
    private DataLayer? dataLayer;
    private Task<SqliteConnection>? dbTask;

    internal AppUtilities(Frame frame, Task<SqliteConnection> connection)
    {
        this.dbTask = connection;
        this.frame = frame;
    }

    /// <inheritdoc />
    public void ShowLogin() => this.ShowPlaceholder("Login");

    /// <inheritdoc/>
    public void ShowList()
    {
        this.ShowPlaceholder("List");
    }

    /// <inheritdoc/>
    public void ShowPlaceholder(object? parameter = null)
    {
        async Task<IFolderDatabase> LocalWork()
        {
            var dataLayer = await this.GetDataLayer();
            return dataLayer.Folders;
        }

        var placeholderParameter = new PlaceholderParameter(parameter ?? placeholderCount++, LocalWork());

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

            // Create our data layer
            var events = new DispatcherDatabaseEvents(this.frame.DispatcherQueue);
            var articleDB = InstapaperDatabase.GetArticleDatabase(dbConnection, events);
            var folderDB = InstapaperDatabase.GetFolderDatabase(dbConnection, events);
            var databases = new DataLayer(articleDB, folderDB, events, dbConnection);

            lock (this.dataLayerLock)
            {
                Debug.Assert(this.dataLayer == null);
                this.dataLayer = databases;
            }

            return databases;
        }

        // Opportunistically check if we've got the datalayer. If we have, we
        // don't need to perform any operations, we can just return the result.
        var localDataLayer = this.dataLayer;
        Task<DataLayer>? localDataLayerTask = null;
        if (localDataLayer != null)
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

        this.disposed = true;

        // Yes, I know this will block. Yes, thats the point.
        var localDataLayer = this.dataLayerTask?.Result;
        this.dataLayerTask?.Dispose();

        if (localDataLayer != null)
        {
            localDataLayer.Connection.Close();
            localDataLayer.Connection.Dispose();
        }

        this.dataLayer = null;
    }
}
