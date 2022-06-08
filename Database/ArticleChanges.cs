using System.Data;

namespace Codevoid.Storyvoid;

/// <inheritdoc />
public class ArticleChanges : IArticleChangesDatabase
{
    private IDbConnection connection;

    private ArticleChanges(IDbConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>
    /// For the supplied DB connection, get an instance of the Pending Article
    /// Changes API.
    /// </summary>
    /// <param name="connection">
    /// The opened DB Connection to use to access the database.
    /// </param>
    /// <returns>Instance of the the API</returns>
    public static IArticleChangesDatabase GetPendingArticleChangeDatabase(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Database must be opened");
        }

        return new ArticleChanges(connection);
    }
}