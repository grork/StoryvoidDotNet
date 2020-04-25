using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    [Order(2), Collection(TestUtilities.TestCollectionName)]
    public class FoldersTests
    {
        private CurrentServiceStateFixture SharedState;
        public FoldersTests(CurrentServiceStateFixture state)
        {
            this.SharedState = state;
        }

        [Fact, Order(0)]
        public async Task CanAddFolder()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            var createdFolder = await client.Add(folderName);
            Assert.Equal(folderName, createdFolder.Title);
        }

        [Fact, Order(1)]
        public async Task CanListFolders()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            await client.List();
        }

    }
}
