using System;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    public class BookmarksHaveTests
    {
        [Fact]
        public void HaveWithOnlyIdReturnsJustTheId()
        {
            HaveStatus status = new HaveStatus("1234");
            Assert.Equal("1234", status.ToString());
        }

        [Theory]
        [InlineData("")] // Empty String
        [InlineData("\t   ")] // Whitespace
        public void EmptyIdThrows(string idData)
        {
            Assert.Throws<ArgumentNullException>(() => _ = new HaveStatus(idData));
        }

        [Fact]
        public void HaveWithIdAndHashReturnsCorrectString()
        {
            HaveStatus status = new HaveStatus("12345", "OjMuzFp6");
            Assert.Equal("12345:OjMuzFp6", status.ToString());
        }

        [Theory]
        [InlineData("", "HASH")] // ID Empty, has hash
        [InlineData("\t   ", "HASH")] // ID whitespace, has hash
        [InlineData("1234", "")] // Has ID, empty hash
        [InlineData("1234", "\t    ")] // Has ID, whitespace hash
        public void EmptyHashOrIdThrows(string id, string hash)
        {
            Assert.Throws<ArgumentNullException>(() => _ = new HaveStatus(id, hash));
        }

        [Fact]
        public void HaveStatusCorrectlyStringifysProgressAndTimestamp()
        {
            HaveStatus status = new HaveStatus("12345", "OjMuzFp6", 0.5, DateTimeOffset.FromUnixTimeSeconds(1288584076).LocalDateTime);
            Assert.Equal("12345:OjMuzFp6:0.5:1288584076", status.ToString());
        }
    }
}
