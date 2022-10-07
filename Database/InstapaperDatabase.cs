using System.Data;

namespace Codevoid.Storyvoid;

internal sealed class WithinTransactionArgs<TPayload> : EventArgs
{
    internal TPayload Data { get; private set; }
    internal IDbConnection Connection { get; private set; }

    internal WithinTransactionArgs(IDbConnection transaction, TPayload data)
    {
        this.Data = data;
        this.Connection = transaction;
    }
}

internal delegate void WithinTransactionEventHandler<TSender, TPayload>(TSender sender, WithinTransactionArgs<TPayload> e);

internal struct EventCleanupHelper : IDisposable
{
    private Action? detach;
    internal EventCleanupHelper(Action attach, Action detach)
    {
        attach();
        this.detach = detach;
    }

    public void Dispose()
    {
        if (this.detach is null)
        {
            return;
        }

        this.detach();
        this.detach = null;
    }
}

internal static class EventCleanupHelperExtensions
{
    public static void Add(this IList<EventCleanupHelper> instance, Action attach, Action detach)
    {
        instance.Add(new(attach, detach));
    }

    public static void DetachHandlers(this IList<EventCleanupHelper> instance)
    {
        foreach (var h in instance)
        {
            h.Dispose();
        }

        instance.Clear();
    }
}

public static class InstapaperDatabase
{
    private const int CURRENT_DB_VERSION = 1;

    /// <summary>
    /// Checks if the supplied version is the version of the DB we are
    /// are currently expecting
    /// </summary>
    /// <param name="version">Version to compare about</param>
    /// <returns>True if the version exactly matches</returns>
    private static bool IsCurrentDBVersion(int version)
    {
        return (CURRENT_DB_VERSION == version);
    }

    public static void CreateDatabaseIfNeeded(this IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be opened");
        }

        // Perform any migrations
        using (var checkIfUpdated = connection.CreateCommand("PRAGMA user_version"))
        {
            var databaseVersion = Convert.ToInt32(checkIfUpdated.ExecuteScalar());
            if (!IsCurrentDBVersion(databaseVersion))
            {
                // Database is not the right version, so do a migration
                var migrateQueryText = File.ReadAllText("migrations/v0-to-v1.sql");

                using var migrate = connection.CreateCommand(migrateQueryText);
                var result = migrate.ExecuteNonQuery();
                if (result == 0)
                {
                    throw new InvalidOperationException("Unable to create database");
                }

                // Since there may be subtle syntax errors in the
                // migration file that do not cause an outright failure
                // in execution, we leverage the fact that the version
                // bump of the DB is at the end of the script. If the
                // version doesn't match, we'll fail.
                databaseVersion = Convert.ToInt32(checkIfUpdated.ExecuteScalar());
                if (!IsCurrentDBVersion(databaseVersion))
                {
                    throw new InvalidOperationException("Unable to create database");
                }
            }
        }
    }

    public static IFolderDatabase GetFolderDatabase(this IDbConnection connection, IDatabaseEventSource? eventSource = null)
    {
        return new FolderDatabase(connection, eventSource);
    }

    public static IFolderChangesDatabase GetFolderChangesDatabase(this IDbConnection connection)
    {
        return new FolderChanges(connection);
    }

    public static IArticleDatabase GetArticleDatabase(this IDbConnection connection, IDatabaseEventSource? eventSource = null)
    {
        return new ArticleDatabase(connection, eventSource);
    }

    public static IArticleChangesDatabase GetArticleChangesDatabase(this IDbConnection connection)
    {
        return new ArticleChanges(connection);
    }

    public static IDisposable GetLedger(IFolderDatabase folderDb, IArticleDatabase articleDb)
    {
        IFolderDatabaseWithTransactionEvents? folderDbWithEvents = folderDb as IFolderDatabaseWithTransactionEvents;
        IArticleDatabaseWithTransactionEvents? articleDbWithEvents = articleDb as IArticleDatabaseWithTransactionEvents;
        if (folderDbWithEvents is null)
        {
            throw new ArgumentException("Folder database must support events to use the ledger", nameof(folderDb));
        }

        if (articleDbWithEvents is null)
        {
            throw new ArgumentException("Article database must support events to use the ledger", nameof(articleDb));
        }

        return new Ledger(folderDbWithEvents, articleDbWithEvents);
    }
}