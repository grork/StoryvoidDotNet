namespace Codevoid.Storyvoid.ViewModels.Commands;

internal class ArchiveCommand : ArticleCommand
{
    public ArchiveCommand(IArticleDatabase database) : base(database)
    { }

    protected override void CoreExecute(DatabaseArticle article)
    {
        this.database.MoveArticleToFolder(article.Id, WellKnownLocalFolderIds.Archive);
    }
}