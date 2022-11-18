using Codevoid.Storyvoid.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace Codevoid.Test.Storyvoid;

[TestClass]
public class AppUtilitiesTests
{
    [UITestMethod]
    public void CanInstantiate()
    {
        var utilities = new AppUtilities(
            App.Instance!.TestWindow!.Frame,
            Task.FromResult(new SqliteConnection())
        );

        Assert.IsNotNull(utilities);
    }
}