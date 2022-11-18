using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace Codevoid.Test.Storyvoid;

[TestClass]
public class AppUtilitiesTests
{
    [UITestMethod]
    public void FirstTest()
    {
        Assert.AreEqual(0, 0);
    }
}