using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

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

    private AppUtilities GetAppUtilities()
    {
        return new AppUtilities(
            App.Instance!.TestWindow!.Frame,
            this.connectionTask.Value
        );
    }

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
            App.Instance!.TestWindow!.Frame,
            this.connectionTask.Value
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
                App.Instance!.TestWindow!.Frame,
                ConnectionWithDelay()
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
        var datalayer = await utilities.GetDataLayer();
        utilities.Dispose();

        datalayer = await utilities.GetDataLayer();
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
}