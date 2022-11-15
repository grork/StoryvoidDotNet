
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
        Assert.True(this.helper.command.CanExecute(articleToDelete));

        this.helper.command.Execute(articleToDelete);

        Assert.Null(this.helper.articleDatabase.GetArticleById(articleToDelete.Id));
    }

    [Fact]
    public void DeletingMissingArticleDoesntFail()
    {
        var articleToDelete = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        this.helper.articleDatabase.DeleteArticle(articleToDelete.Id);

        Assert.True(this.helper.command.CanExecute(articleToDelete));

        this.helper.command.Execute(articleToDelete);
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
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);
        var destination = this.helper.folderDatabase.FirstCompleteUserFolder().LocalId;

        this.helper.command.DestinationLocalFolderId = destination;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleInDestinationFolder = this.helper.articleDatabase.ListArticlesForLocalFolder(destination).First((a) => articleToMove.Id == a.Id);
        Assert.NotNull(articleInDestinationFolder);
    }

    [Fact]
    public void MovingArticleWithInvalidFolderDoesNothing()
    {
        var DESTINATION = -1L;
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        this.helper.command.DestinationLocalFolderId = DESTINATION;
        Assert.False(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove.Id);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }

    [Fact]
    public void MovingArticleToNonexistantFolderDoesntThrow()
    {
        var destination = this.helper.folderDatabase.ListAllCompleteUserFolders().Max((f) => f.LocalId) + 1;
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        this.helper.command.DestinationLocalFolderId = destination;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove.Id);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }

    [Fact]
    public void MovingArticleToTheFolderItIsAlreadyInDoesNothing()
    {
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        this.helper.command.DestinationLocalFolderId = WellKnownLocalFolderIds.Unread;
        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Unread).First((a) => a.Id == articleToMove.Id);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }
}

public class ArchiveCommandTests : IDisposable
{
    private class Helper : CommandTestHelper<ArchiveCommand>
    {
        protected override ArchiveCommand MakeCommand() => new ArchiveCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    [Fact]
    public void CanArchiveArticle()
    {
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Unread);

        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleInDestinationFolder = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).First((a) => articleToMove.Id == a.Id);
        Assert.NotNull(articleInDestinationFolder);
    }

    [Fact]
    public void ArchivingArticleThatIsAlreadyArchivedDoesNothing()
    {
        var articleToMove = this.helper.articleDatabase.FirstArticleInFolder(WellKnownLocalFolderIds.Archive);

        Assert.True(this.helper.command.CanExecute(articleToMove));

        this.helper.command.Execute(articleToMove);

        var articleThatShouldntHaveMoved = this.helper.articleDatabase.ListArticlesForLocalFolder(WellKnownLocalFolderIds.Archive).First((a) => a.Id == articleToMove.Id);
        Assert.NotNull(articleThatShouldntHaveMoved);
    }
}

public class LikeCommandTests : IDisposable
{
    private class Helper : CommandTestHelper<LikeCommand>
    {
        protected override LikeCommand MakeCommand() => new LikeCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    [Fact]
    public void CanLikeDislikedArticle()
    {
        var articleToLike = this.helper.articleDatabase.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        Assert.True(this.helper.command.CanExecute(articleToLike));

        this.helper.command.Execute(articleToLike);

        var postLike = this.helper.articleDatabase.GetArticleById(articleToLike.Id)!;
        Assert.True(postLike.Liked);
    }

    [Fact]
    public void LikedArticleCantBeExecuted()
    {
        var articleToLike = this.helper.articleDatabase.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        articleToLike = this.helper.articleDatabase.LikeArticle(articleToLike.Id);

        Assert.False(this.helper.command.CanExecute(articleToLike));
    }
}

public class UnlikeCommandTests : IDisposable
{
    private class Helper : CommandTestHelper<UnlikeCommand>
    {
        protected override UnlikeCommand MakeCommand() => new UnlikeCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    [Fact]
    public void CanUnlikeLikedArticle()
    {
        var articleToUnlike = this.helper.articleDatabase.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);
        articleToUnlike = this.helper.articleDatabase.LikeArticle(articleToUnlike.Id);

        Assert.True(this.helper.command.CanExecute(articleToUnlike));

        this.helper.command.Execute(articleToUnlike);

        var postLike = this.helper.articleDatabase.GetArticleById(articleToUnlike.Id)!;
        Assert.False(postLike.Liked);
    }

    [Fact]
    public void UnLikedArticleCantBeExecuted()
    {
        var articleToUnlike = this.helper.articleDatabase.FirstUnlikedArticleInfolder(WellKnownLocalFolderIds.Unread);

        Assert.False(this.helper.command.CanExecute(articleToUnlike));
    }
}