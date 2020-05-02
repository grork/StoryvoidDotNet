using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codevoid.Instapaper;
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
        #region IAsyncLifetime
        private IMessageSink logger;

        private void LogMessage(string message)
        {
            this.logger.OnMessage(new DiagnosticMessage(message));
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            LogMessage("Starting Cleanup");
            await Task.CompletedTask;
            LogMessage("Completing Cleanup");
        }

        async Task IAsyncLifetime.InitializeAsync()
        {
            LogMessage("Starting Init");
            await Task.CompletedTask;
            LogMessage("Completing Init");
        }
        #endregion

        public CurrentServiceStateFixture(IMessageSink loggerInstance)
        {
            this.logger = loggerInstance;
            this.Folders = new List<IFolder>();
        }

        public IList<IFolder> Folders { get; }

        internal void ReplaceFolderList(IEnumerable<IFolder> folders)
        {
            this.Folders.Clear();
            foreach (var folder in folders)
            {
                this.Folders.Add(folder);
            }
        }

        private IFoldersClient? _foldersClient;
        public IFoldersClient FoldersClient
        {
            get
            {
                if (this._foldersClient == null)
                {
                    this._foldersClient = new FoldersClient(TestUtilities.GetClientInformation());
                }

                return this._foldersClient;
            }
        }
    }
}
