
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels.Commands;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class DeleteCommandTests : IDisposable
{
    private class Helper : CommandTestHelper<DeleteCommand>
    {
        protected override DeleteCommand MakeCommand() => new DeleteCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    [Fact]
    public void CanDeleteArticle()
    {
        var articleToDelete = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        Assert.True(this.helper.command.CanExecute(articleToDelete.Id));

        this.helper.command.Execute(articleToDelete.Id);

        Assert.Null(this.helper.articleDatabase.GetArticleById(articleToDelete.Id));
    }

    [Fact]
    public void DeletingMissingArticleDoesntFail()
    {
        var articleIdToDelete = this.helper.articleDatabase.ListAllArticlesInAFolder().Max((a) => a.Article.Id) + 1;

        Assert.True(this.helper.command.CanExecute(articleIdToDelete));

        this.helper.command.Execute(articleIdToDelete);
    }
}

public class MoveCommandTests : IDisposable
{
    private class Helper : CommandTestHelper<MoveCommand>
    {
        protected override MoveCommand MakeCommand() => new MoveCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    [Fact]
    public void CanMoveArticle()
    {
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread)!.Id;
        var destination = this.helper.folderDatabase.FirstCompleteUserFolder().LocalId;

        this.helper.command.DestinationLocalFolderId = destination;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleInDestinationFolder = this.helper.articleDatabase.ListArticlesForLocalFolder(destination).First((a) => articleToMove == a.Id);
        Assert.NotNull(articleInDestinationFolder);
    }

    [Fact]
    public void MovingArticleWithInvalidFolderDoesNothing()
    {
        var DESTINATION = -1L;
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread)!.Id;

        this.helper.command.DestinationLocalFolderId = DESTINATION;
        Assert.False(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }

    [Fact]
    public void MovingArticleToNonexistantFolderDoesntThrow()
    {
        var destination = this.helper.folderDatabase.ListAllCompleteUserFolders().Max((f) => f.LocalId) + 1;
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread)!.Id;

        this.helper.command.DestinationLocalFolderId = destination;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }

    [Fact]
    public void MovingArticleToTheFolderItIsAlreadyInDoesNothing()
    {
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread)!.Id;

        this.helper.command.DestinationLocalFolderId = WellKnownLocalFolderIds.Unread;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }
}