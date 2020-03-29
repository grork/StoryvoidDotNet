using System;
using Codevoid.Instapaper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codevoid.Test.Instapaper
{
    [TestClass]
    public class BookmarksHaveTests
    {
        [TestMethod]
        public void HaveWithOnlyIdReturnsJustTheId()
        {
            HaveStatus status = new HaveStatus("1234");
            Assert.AreEqual("1234", status.ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        [DataRow("", DisplayName = "Empty String")]
        [DataRow("\t   ", DisplayName = "Whitespace")]
        public void EmptyIdThrows(string idData)
        {
            HaveStatus status = new HaveStatus(idData);
        }

        [TestMethod]
        public void HaveWithIdAndHashReturnsCorrectString()
        {
            HaveStatus status = new HaveStatus("12345", "OjMuzFp6");
            Assert.AreEqual("12345:OjMuzFp6", status.ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        [DataRow("", "HASH", DisplayName = "ID Empty, has hash")]
        [DataRow("\t   ", "HASH", DisplayName = "ID whitespace, has hash")]
        [DataRow("1234", "", DisplayName = "Has ID, empty hash")]
        [DataRow("1234", "\t    ", DisplayName = "Has ID, whitespace hash")]
        public void EmptyHashOrIdThrows(string id, string hash)
        {
            HaveStatus status = new HaveStatus(id, hash);
        }

        [TestMethod]
        public void HaveStatusCorrectlyStringifysProgressAndTimestamp()
        {
            HaveStatus status = new HaveStatus("12345", "OjMuzFp6", 0.5, DateTimeOffset.FromUnixTimeSeconds(1288584076));
            Assert.AreEqual("12345:OjMuzFp6:0.5:1288584076", status.ToString());
        }
    }
}
