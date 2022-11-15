namespace Codevoid.Storyvoid.ViewModels.Commands;

internal class ArchiveCommand : ArticleCommand
{
    public ArchiveCommand(IArticleDatabase database) : base(database)
    { }
    
    protected override void CoreExecute(long articleId)
    {
        this.database.MoveArticleToFolder(articleId, WellKnownLocalFolderIds.Archive);
    }
}