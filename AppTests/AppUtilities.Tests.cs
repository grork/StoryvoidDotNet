using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Storage;

namespace Codevoid.Test.Storyvoid;

[TestClass]
public class AppUtilitiesTests
{
    private static Task<SqliteConnection> GetDatabaseTask()
    {
        return Task.Run(() =>
        {
            var connection = new SqliteConnection("Data Source=StaysInMemory;Mode=Memory;Cache=Shared");
            connection.Open();
            connection.CreateDatabaseIfNeeded();

            return connection;
        });
    }

    private Lazy<Task<SqliteConnection>> connectionTask = new Lazy<Task<SqliteConnection>>(GetDatabaseTask);

    private class TestAppNavigation : IAppNavigation
    {
        public void ClearStack() { }

        public void ShowList(ArticleList articleList) { }

        public void ShowLogin(Authenticator authenticator) { }

        public void ShowPlaceholder(NavigationParameter navigationParameter) { }
        public void ShowSigningOut() { }
    }

    private AppUtilities GetAppUtilities()
    {
        return new AppUtilities(
            new TestAppNavigation(),
            this.connectionTask.Value,
            this.DispatcherQueue
        );
    }

    private DispatcherQueue DispatcherQueue => App.Instance!.TestWindow!.DispatcherQueue;

    [TestCleanup]
    public void Cleanup()
    {
        var connection = this.connectionTask.Value.Result;
        connection.Close();
        connection.Dispose();

        this.connectionTask = new Lazy<Task<SqliteConnection>>(GetDatabaseTask);
    }

    [UITestMethod]
    public void CanInstantiate()
    {
        var utilities = new AppUtilities(
            new TestAppNavigation(),
            this.connectionTask.Value,
            this.DispatcherQueue
        );

        Assert.IsNotNull(utilities);
    }

    [TestMethod]
    public async Task CanGetDataLayerFromUtilities()
    {
        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();

        Assert.IsTrue(App.Instance!.TestWindow!.DispatcherQueue.HasThreadAccess);
        var utilities = this.GetAppUtilities();
        var dataLayer = await utilities.GetDataLayer();

        // Check that we actually got something from the database
        Assert.AreEqual(2, dataLayer.Folders.ListAllFolders().Count());
    }

    [TestMethod]
    public async Task SimultanouslyishGettingDataLayerReturnsSingleInstance()
    {
        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();
        var utilities = this.GetAppUtilities();
        var dataLayerTask1 = utilities.GetDataLayer();
        var dataLayerTask2 = utilities.GetDataLayer();

        var dataLayer1 = await dataLayerTask1;
        var dataLayer2 = await dataLayerTask2;

        Assert.AreSame(dataLayer1, dataLayer2);
        Assert.IsTrue(object.ReferenceEquals(dataLayer1, dataLayer2));
    }

    [TestMethod]
    public async Task SimultanouslyishGettingDataLayerReturnsSingleInstanceWithDelayInGettingDatabase()
    {
        async Task<SqliteConnection> ConnectionWithDelay()
        {
            var db = await GetDatabaseTask();
            await Task.Delay(100);
            return db;
        }

        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();
        var utilities = new AppUtilities(
                new TestAppNavigation(),
                ConnectionWithDelay(),
                this.DispatcherQueue
            );

        var dataLayerTask1 = utilities.GetDataLayer();
        var dataLayerTask2 = utilities.GetDataLayer();

        var dataLayer1 = await dataLayerTask1;
        var dataLayer2 = await dataLayerTask2;

        Assert.AreSame(dataLayer1, dataLayer2);
        Assert.IsTrue(object.ReferenceEquals(dataLayer1, dataLayer2));
    }

    [TestMethod]
    public async Task DisposingDatalayerPreventsGettingDatabaseAgain()
    {
        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();
        var utilities = this.GetAppUtilities();
        _ = await utilities.GetDataLayer();
        utilities.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = await utilities.GetDataLayer());
    }

    [TestMethod]
    public async Task LedgerCreatedAndCapturesChanges()
    {
        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();

        var utilities = this.GetAppUtilities();
        var dataLayer = await utilities.GetDataLayer();
        var connection = await this.connectionTask.Value;
        var folderChanges = InstapaperDatabase.GetFolderChangesDatabase(connection);

        Assert.AreEqual(0, folderChanges.ListPendingFolderAdds().Count());
        dataLayer.Folders.CreateFolder(DateTime.Now.Ticks.ToString());
        Assert.AreEqual(1, folderChanges.ListPendingFolderAdds().Count());
    }

    [TestMethod]
    public async Task DatabaseCanBeDeletedAfterClosingDatabase()
    {
        // Cleanup the in-memory database since we don't want that, we want
        // a real, local file
        var connection = this.connectionTask.Value.Result;
        connection.Close();
        connection.Dispose();

        this.connectionTask = new Lazy<Task<SqliteConnection>>(Task.Run(AppUtilities.OpenDatabaseAsync));

        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();

        string datasourcePath = String.Empty;
        using (var utilities = this.GetAppUtilities())
        {
            var dataLayer = await utilities.GetDataLayer();
            datasourcePath = dataLayer.Connection.DataSource;
            Assert.IsTrue(File.Exists(datasourcePath));
            Assert.IsNotNull(dataLayer.Articles);
        }

        AppUtilities.DeleteLocalFiles();
        Assert.IsFalse(File.Exists(datasourcePath));
    }

    [TestMethod]
    public async Task SigningOutThenSigniningInGetsNewDatabaseSuccessfully()
    {
        await DispatcherQueueThreadSwitcher.SwitchToDispatcher();
        var utilities = this.GetAppUtilities();
        var ogDataLayer = await utilities.GetDataLayer();
        await utilities.Signout();

        var newDataLayer = await utilities.GetDataLayer();
        Assert.AreNotEqual(ogDataLayer, newDataLayer);


        Assert.IsTrue(newDataLayer.Folders.ListAllFolders().Any(), "Should be able to run a query and find *some* folders");
    }
}