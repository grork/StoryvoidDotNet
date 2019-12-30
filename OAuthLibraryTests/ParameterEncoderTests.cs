using Codevoid.Utilities.OAuth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codevoid.Test.Utilities.OAuth
{
    [TestClass]
    public class ParameterEncoderTests
    {
        [TestMethod]
        public void CanConstructParameterEncoder()
        {
            var instance = new ParameterEncoder();
            Assert.IsNotNull(instance, "Encoder instance was null");
        }
    }
}