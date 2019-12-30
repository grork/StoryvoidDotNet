using System.Collections.Generic;
using Codevoid.Utilities.OAuth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codevoid.Test.Utilities.OAuth
{
    [TestClass]
    public class ParameterEncoderTests
    {
        [TestMethod]
        public void OneParameterAndValueEncodes()
        {
            var sampleData = new Dictionary<string, string>
            {
                { "a", "b" }
            };

            var result = ParameterEncoder.FormEncodeValues(sampleData);
            Assert.AreEqual("a=b", result, "Encoding string didn't match");
        }

        [TestMethod]
        public void TwoParametersAndValueEncodesWithCorrectOrder()
        {
            var sampleData = new Dictionary<string, string>
            {
                { "b", "c%jkt" },
                { "a", "b" }
            };

            var result = ParameterEncoder.FormEncodeValues(sampleData);
            Assert.AreEqual("a=b&b=c%25jkt", result, "Encoding string didn't match");
        }

        [TestMethod]
        public void ValuesAreEncodedAccordingToRFC3986()
        {
            var sampleData = new Dictionary<string, string>
            {
                { "!'()*", "*)('!" }
            };

            var result = ParameterEncoder.FormEncodeValues(sampleData);
            Assert.AreEqual("%21%27%28%29%2A=%2A%29%28%27%21", result, "Encoding string didn't match");
        }

        [TestMethod]
        public void CustomDelimiterIsRespectedIfProvided()
        {
            var sampleData = new Dictionary<string, string>
            {
                { "b", "c%jkt" },
                { "a", "b" }
            };

            var result = ParameterEncoder.FormEncodeValues(sampleData, delimiter: ", ");
            Assert.AreEqual("a=b, b=c%25jkt", result, "Encoding string didn't match");
        }

        [TestMethod]
        public void ValuesAreQuotedWhenRequested()
        {
            var sampleData = new Dictionary<string, string>
            {
                { "b", "c%jkt" },
                { "a", "b" }
            };

            var result = ParameterEncoder.FormEncodeValues(sampleData, shouldQuoteValues: true);
            Assert.AreEqual("a=\"b\"&b=\"c%25jkt\"", result, "Encoding string didn't match");
        }
    }
}