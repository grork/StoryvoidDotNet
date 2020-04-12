using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;

namespace Codevoid.Test.Instapaper
{
    public class BookmarksTests
    {
        [Fact]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var client = new BookmarksClient(TestUtilities.GetClientInformation());
            await client.List(WellKnownFolderIds.Unread);
        }
    }
}