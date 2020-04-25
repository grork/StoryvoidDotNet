using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    [Order(4), Collection(TestUtilities.TestCollectionName)]
    public class BookmarksTests
    {
        private CurrentServiceStateFixture sharedState;
        public BookmarksTests(CurrentServiceStateFixture state)
        {
            this.sharedState = state;

        }
        [Fact]
        public async Task CanSuccessfullyListUnreadFolder()
        {
            var client = new BookmarksClient(TestUtilities.GetClientInformation());
            await client.List(WellKnownFolderIds.Unread);
        }
    }
}