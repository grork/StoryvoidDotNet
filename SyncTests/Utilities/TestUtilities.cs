using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace Codevoid.Test.Storyvoid;

internal static class SyncTestUtilities
{
    internal static (SqliteConnection Connection,
                     MockFolderService Folders,
                     MockBookmarksService Bookmarks) GetService()
    {
        // Create a copy of that database, which will serve as the starting
        // point for the service database.
        var (serviceConnection, serviceFolderDb, _, serviceArticleDb, _) = TestUtilities.GetEmptyDatabase();
        return (
            serviceConnection,
            new MockFolderService(serviceFolderDb, serviceArticleDb),
            new MockBookmarksService(serviceArticleDb, serviceFolderDb)
        );
    }
}

public abstract class BaseSyncTest : IDisposable
{
    protected const int DEFAULT_FOLDER_COUNT = 2;
    private IDbConnection connection;
    private IDbConnection serviceConnection;

    protected (
        IFolderDatabase FolderDB,
        IFolderChangesDatabase FolderChangesDB,
        IArticleDatabase ArticleDB,
        IArticleChangesDatabase ArticleChangesDB
    ) databases;

    protected (
        MockFolderService FoldersClient,
        MockBookmarksService BookmarksClient
    ) service;

    protected InstapaperSync syncEngine;

    protected BaseSyncTest()
    {
        var databases = TestUtilities.GetDatabases();
        var service = SyncTestUtilities.GetService();
        databases.Connection.BackupDatabase(service.Connection);

        this.connection = databases.Connection;
        this.databases = (databases.FolderDB, databases.FolderChangeDB, databases.ArticleDB, databases.ArticleChangeDB);

        this.serviceConnection = service.Connection;
        this.service = (service.Folders, service.Bookmarks);

        this.SetSyncEngineFromDatabases();
    }

    [MemberNotNull(nameof(syncEngine))]
    internal void SetSyncEngineFromDatabases(IDatabaseSyncEventSource? clearingHouse = null)
    {
        this.syncEngine = new InstapaperSync(
            this.databases.FolderDB,
            this.databases.FolderChangesDB,
            this.service.FoldersClient,
            this.databases.ArticleDB,
            this.databases.ArticleChangesDB,
            this.service.BookmarksClient,
            clearingHouse
        );
    }

    protected void SwitchToEmptyLocalDatabase()
    {
        this.DisposeLocalDatabase();
        var databases = TestUtilities.GetEmptyDatabase();
        this.connection = databases.Connection;
        this.databases = (databases.FolderDB, databases.FolderChangeDB, databases.ArticleDB, databases.ArticleChangeDB);
        this.SetSyncEngineFromDatabases();

        // Make sure we have an empty database for this test.
        Assert.Equal(DEFAULT_FOLDER_COUNT, this.databases.FolderDB.ListAllFolders().Count());
    }

    protected void SwitchToEmptyServiceDatabase()
    {
        this.DisposeServiceDatabase();

        var service = SyncTestUtilities.GetService();
        this.serviceConnection = service.Connection;
        this.service = (service.Folders, service.Bookmarks);
        Assert.Empty(service.Folders.FolderDB.ListAllCompleteUserFolders());

        this.SetSyncEngineFromDatabases();
    }

    protected IDbTransaction StartTransactionForLocalDatabase()
    {
        return this.connection.BeginTransaction();
    }

    protected IDisposable GetLedger()
    {
        return InstapaperDatabase.GetLedger(this.databases.FolderDB, this.databases.ArticleDB);
    }

    private void DisposeLocalDatabase()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    private void DisposeServiceDatabase()
    {
        this.serviceConnection.Close();
        this.serviceConnection.Dispose();
    }

    public void Dispose()
    {
        this.DisposeLocalDatabase();
        this.DisposeServiceDatabase();
    }
}