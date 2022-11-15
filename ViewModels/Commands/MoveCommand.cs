namespace Codevoid.Storyvoid.ViewModels.Commands;

/// <summary>
/// Moves an article to the supplied destination local folder. If the folder 
/// doesn't exist, it will silently No-op.
/// </summary>
internal class MoveCommand : ArticleCommand
{
    /// <summary>
    /// Local folder ID that the article will be moved into
    /// </summary>
    public long DestinationLocalFolderId { get; set; }

    public MoveCommand(IArticleDatabase database) : base(database)
    { }

    /// <inheritdoc />
    protected override bool CoreCanExecute(DatabaseArticle article)
    {
        return base.CoreCanExecute(article) && this.DestinationLocalFolderId > 0;
    }

    /// <inheritdoc />
    protected override void CoreExecute(DatabaseArticle article)
    {
        try
        {
            this.database.MoveArticleToFolder(article.Id, this.DestinationLocalFolderId);
        }
        catch (FolderNotFoundException)
        // When the folder doesn't exist, an exception is raised. We don't want
        // to surface that to anyone, so we're going to silent eat it.
        { }
    }
}