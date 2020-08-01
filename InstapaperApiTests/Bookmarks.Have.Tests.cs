using System;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    public sealed class BookmarksHaveTests
    {
        [Fact]
        public void HaveWithOnlyIdReturnsJustTheId()
        {
            HaveStatus status = new HaveStatus(1234UL);
            Assert.Equal("1234", status.ToString());
        }

        [Fact]
        public void EmptyIdThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HaveStatus(0UL));
        }

        [Fact]
        public void HaveWithIdAndHashReturnsCorrectString()
        {
            HaveStatus status = new HaveStatus(12345UL, "OjMuzFp6");
            Assert.Equal("12345:OjMuzFp6", status.ToString());
        }

        [Theory]
        [InlineData(0UL, "HASH")] // ID Empty, has hash
        [InlineData(1234UL, "")] // Has ID, empty hash
        [InlineData(1234UL, "\t    ")] // Has ID, whitespace hash
        public void EmptyHashOrIdThrows(ulong id, string hash)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HaveStatus(id, hash));
        }

        [Fact]
        public void HaveStatusCorrectlyStringifysProgressAndTimestamp()
        {
            HaveStatus status = new HaveStatus(12345UL, "OjMuzFp6", 0.5F, DateTimeOffset.FromUnixTimeSeconds(1288584076).LocalDateTime);
            Assert.Equal("12345:OjMuzFp6:0.5:1288584076000", status.ToString());
        }
    }
}
