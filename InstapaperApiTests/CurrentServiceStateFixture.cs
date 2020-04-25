using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Codevoid.Test.Instapaper
{
    /// <summary>
    /// Fixture class to orchestrate initilization of service state. Lives through
    /// the full life of the test collection, and captures things such as (but not
    /// limited to):
    /// - Current Folders
    /// - Current Remote Bookmarks
    /// - Current Bookmark State
    /// - Added Bookmark count
    /// </summary>
    public class CurrentServiceStateFixture : IAsyncLifetime
    {
        private IMessageSink logger;

        public CurrentServiceStateFixture(IMessageSink loggerInstance)
        {
            this.logger = loggerInstance;
        }

        private void LogMessage(string message)
        {
            this.logger.OnMessage(new DiagnosticMessage(message));
        }

        public async Task DisposeAsync()
        {
            LogMessage("Starting Cleanup");
            await Task.CompletedTask;
            LogMessage("Completing Cleanup");
        }

        public async Task InitializeAsync()
        {
            LogMessage("Starting Init");
            await Task.CompletedTask;
            LogMessage("Completing Init");
        }
    }
}
