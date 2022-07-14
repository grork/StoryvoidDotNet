using Codevoid.Storyvoid;

namespace Codevoid.Test.Storyvoid;

public class EverythingSyncTests : BaseSyncTest
{
    private void AssertServerAndClientMatch()
    {
        // Check folders match
        var serviceFolders = this.service.FoldersClient.FolderDB.ListAllFolders();
        var localFolders = this.databases.FolderDB.ListAllFolders();
        Assert.Equal(serviceFolders, localFolders, new CompareFoldersIgnoringLocalId());

        // Check Articles match
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListAllArticles(), this.databases.ArticleDB.ListAllArticles());
        Assert.Equal(this.service.BookmarksClient.ArticleDB.ListLikedArticles(), this.databases.ArticleDB.ListLikedArticles());

        // Check those articles are in the right folders
        foreach (var serviceFolder in serviceFolders)
        {
            var localFolder = this.databases.FolderDB.GetFolderByServiceId(serviceFolder.ServiceId!.Value)!;
            var serviceArticles = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFolder.LocalId);
            var localArticles = this.databases.ArticleDB.ListArticlesForLocalFolder(localFolder.LocalId);
            Assert.Equal(serviceArticles, localArticles);
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
    public async Task EmptyServiceEmptiesTheLocalDatabase()
    {
        this.SwitchToEmptyServiceDatabase();

        await this.syncEngine.SyncEverything();

        this.AssertServerAndClientMatch();
    }

    [Fact]
    public async Task LocalAndServiceChangesReconcileDuringAFullSync()
    {
        // Local folder to delete
        var localFolderToDelete = this.databases.FolderDB.FirstCompleteUserFolder();

        // Local article to update
        var localArticleToUpdate = this.databases.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        // Service article that will be updated
        var serviceArticleToUpdate = this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id != localArticleToUpdate.Id)!;

        // Service Folder (and it's local verison) to move an article to
        var serviceFolderToMoveTo = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) => f.ServiceId != localFolderToDelete.ServiceId)!;
        var localFolderToMoveTo = this.databases.FolderDB.GetFolderByServiceId(serviceFolderToMoveTo.ServiceId!.Value)!;

        // Article to move into a service folder
        var serviceArticleToMove = this.service.BookmarksClient.ArticleDB.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);

        // Service folder to be deleted
        var serviceFolderToDelete = this.service.FoldersClient.FolderDB.ListAllCompleteUserFolders().First((f) =>
        {
            return (f.ServiceId != localFolderToDelete.ServiceId) && (f.ServiceId != serviceFolderToMoveTo.ServiceId);
        })!;

        // Perform service changes
        serviceArticleToUpdate = this.service.BookmarksClient.ArticleDB.UpdateReadProgressForArticle(serviceArticleToUpdate.ReadProgress + 0.4F, DateTime.Now, serviceArticleToUpdate.Id);
        serviceArticleToUpdate = this.service.BookmarksClient.ArticleDB.LikeArticle(serviceArticleToUpdate.Id);
        this.service.BookmarksClient.ArticleDB.MoveArticleToFolder(serviceArticleToMove.Id, serviceFolderToMoveTo.LocalId);
        
        foreach(var serviceToDelete in this.service.BookmarksClient.ArticleDB.ListArticlesForLocalFolder(serviceFolderToDelete.LocalId))
        {
            this.service.BookmarksClient.ArticleDB.DeleteArticle(serviceToDelete.Id);
        }

        this.service.FoldersClient.FolderDB.DeleteFolder(serviceFolderToDelete.LocalId);

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