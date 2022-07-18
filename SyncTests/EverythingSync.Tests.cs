using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class EverythingSyncTests : BaseSyncTest
{
    private void AssertServerAndClientMatch()
    {
        // Check folders match
        var remoteFolders = this.service.FoldersClient.FolderDB.ListAllFolders();
        var localFolders = this.databases.FolderDB.ListAllFolders();
        Assert.Equal(remoteFolders, localFolders, new CompareFoldersIgnoringLocalId());

        // Check Articles match
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListAllArticles(), this.databases.ArticleDB.ListAllArticles());
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListLikedArticles(), this.databases.ArticleDB.ListLikedArticles());

        // Check those articles are in the right folders
        foreach (var remoteFolder in remoteFolders)
        {
            var localFolder = this.databases.FolderDB.GetFolderByServiceId(remoteFolder.ServiceId!.Value)!;
            var remoteArticles = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFolder.LocalId);
            var localArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(localFolder.LocalId);
            Assert.Equal(remoteArticles, localArticles);
        }
    }

    [Fact]
    public async Task EmptySyncForDefaultDatabasePullsDownEverythingAndStateMatchesAfter()
    {
        this.SwitchToEmptyLocalDatabase();

        await this.syncEngine.SyncEverything();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task EmptyRemoteEmptiesTheLocalDatabase()
    {
        this.SwitchToEmptyServiceDatabase();

        await this.syncEngine.SyncEverything();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task LocalAndRemoteChangesReconcileDuringAFullSync()
    {
        // Local folder to delete
        var localFolderToDelete = this.databases.FolderDB.FirstCompleteUserFolder();

        // Local article to update
        var localArticleToUpdate = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        // Remote article that will be updated
        var remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id != localArticleToUpdate.Id)!;

        // Remote Folder (and it's local verison) to move an article to
        var remoteFolderToMoveTo = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId != localFolderToDelete.ServiceId)!;
        var localFolderToMoveTo = this.databases.FolderDB.GetFolderByServiceId(remoteFolderToMoveTo.ServiceId!.Value)!;

        // Article to move into a remote folder
        var remoteArticleToMove = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);

        // Remote folder to be deleted
        var remoteFolderToDelete = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) =>
        {
            return (f.ServiceId != localFolderToDelete.ServiceId) && (f.ServiceId != remoteFolderToMoveTo.ServiceId);
        })!;

        // Perform remote changes
        remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(remoteArticleToUpdate.ReadProgress + 0.4F, DateTime.Now, remoteArticleToUpdate.Id);
        remoteArticleToUpdate = this.service.BookmarksClient.ArticleDB.LikeArticle(remoteArticleToUpdate.Id);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(remoteArticleToMove.Id, remoteFolderToMoveTo.LocalId);
        
        foreach(var remoteArticleToDelete in this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(remoteFolderToDelete.LocalId))
        {
            this.service.BookmarksClient.ArticleDB.DeleteArticle(remoteArticleToDelete.Id);
        }

        this.service.FoldersClient.FolderDB.DeleteFolder(remoteFolderToDelete.LocalId);

        // Perform local changes
        using(this.GetLedger())
        {
            this.databases.FolderDB.DeleteFolder(localFolderToDelete.LocalId);
            localArticleToUpdate = this.databases.ArticleDB.UpdateReadProgressForArticle(localArticleToUpdate.ReadProgress + 0.8F, DateTime.Now, localArticleToUpdate.Id);
            localArticleToUpdate = this.databases.ArticleDB.LikeArticle(localArticleToUpdate.Id);
        }

        await this.syncEngine.SyncEverything();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task MegaTransactionCanRollBackLocalDatabaseIfFailureObserved()
    {
        this.SwitchToEmptyServiceDatabase();

        var transaction = this.StartTransactionForLocalDatabase();

        await this.syncEngine.SyncEverything();

        this.AssertServerAndClientMatch();

        transaction.Rollback();
        transaction.Dispose();

        Assert.NotEmpty(this.databases.FolderDB.ListAllCompleteUserFolders());
        Assert.NotEmpty(this.databases.ArticleDB.ListAllArticles());
    }
}