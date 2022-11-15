namespace Codevoid.Storyvoid.ViewModels.Commands;

/// <summary>
/// ICommand implementation that will delete an article from the database
/// </summary>
internal class DeleteCommand : ArticleCommand
{
    public DeleteCommand(IArticleDatabase database) : base(database)
    { }

    /// <inheritdoc />
    protected override void CoreExecute(DatabaseArticle article)
    {
        this.database.DeleteArticle(article.Id);
    }
}