using System.Data;

namespace Codevoid.Storyvoid;

internal static class InstapaperDatabase
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

    public static void CreateDatabaseIfNeeded(IDbConnection connection)
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
}