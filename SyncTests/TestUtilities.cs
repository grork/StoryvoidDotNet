using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Instapaper;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

internal static class TestUtilities
{
    internal static (IDbConnection, IFolderDatabase, IFolderChangesDatabase) GetDatabases()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InstapaperDatabase.CreateDatabaseIfNeeded(connection);

        return (
            connection,
            InstapaperDatabase.GetFolderDatabase(connection),
            InstapaperDatabase.GetFolderChangesDatabase(connection)
        );
    }
}

internal class MockFolders : IFoldersClient
{
    public Task<IInstapaperFolder> AddAsync(string folderTitle)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(long folderId)
    {
        throw new NotImplementedException();
    }

    public Task<IList<IInstapaperFolder>> ListAsync()
    {
        throw new NotImplementedException();
    }
}