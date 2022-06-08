using Codevoid.Instapaper;

namespace Codevoid.Test.Instapaper;

public sealed class BookmarksHaveTests
{
    [Fact]
    public void HaveWithOnlyIdReturnsJustTheId()
    {
        HaveStatus status = new HaveStatus(1234L);
        Assert.Equal("1234", status.ToString());
    }

    [Fact]
    public void EmptyIdThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HaveStatus(0L));
    }

    [Fact]
    public void HaveWithIdAndHashReturnsCorrectString()
    {
        HaveStatus status = new HaveStatus(12345L, "OjMuzFp6");
        Assert.Equal("12345:OjMuzFp6", status.ToString());
    }

    [Theory]
    [InlineData(0UL, "HASH")] // ID Empty, has hash
    [InlineData(1234L, "")] // Has ID, empty hash
    [InlineData(1234L, "\t    ")] // Has ID, whitespace hash
    public void EmptyHashOrIdThrows(long id, string hash)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HaveStatus(id, hash));
    }

    [Fact]
    public void HaveStatusCorrectlyStringifysProgressAndTimestamp()
    {
        HaveStatus status = new HaveStatus(12345L, "OjMuzFp6", 0.5F, DateTimeOffset.FromUnixTimeSeconds(1288584076).LocalDateTime);
        Assert.Equal("12345:OjMuzFp6:0.5:1288584076000", status.ToString());
    }
}