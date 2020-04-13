using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Xunit;

namespace Codevoid.Test.Instapaper
{
    public class FoldersTests
    {
        [Fact]
        public async Task CanListFolders()
        {
            var client = new FoldersClient(TestUtilities.GetClientInformation());
            await client.List();
        }

        [Fact]
        public async Task CanAddFolder()
        {
            var folderName = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var client = new FoldersClient(TestUtilities.GetClientInformation());

            var createdFolder = await client.Add(folderName);
            Assert.Equal(folderName, createdFolder.Title);
        }
    }
}
