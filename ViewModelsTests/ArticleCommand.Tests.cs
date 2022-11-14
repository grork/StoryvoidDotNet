using System.Diagnostics.CodeAnalysis;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels.Commands;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

internal abstract class CommandTestHelper<TCommand> : IDisposable where TCommand : ArticleCommand
{
    private SqliteConnection connection;
    internal IArticleDatabase articleDatabase;
    internal IFolderDatabase folderDatabase;
    internal TCommand command;

    protected CommandTestHelper()
    {
        var (connection, folders, _, articles, _) = TestUtilities.GetDatabases();
        this.connection = connection;
        this.articleDatabase = articles;
        this.folderDatabase = folders;
        this.command = this.MakeCommand();
    }

    protected abstract TCommand MakeCommand();

    public void Dispose()
    {
        this.connection?.Close();
        this.connection?.Dispose();
    }
}

public class ArticleCommandTests : IDisposable
{
    private class MockArticleCommand : ArticleCommand
    {
        public event EventHandler<long>? Executed;

        public MockArticleCommand(IArticleDatabase database) : base(database)
        { }
        
        protected override void CoreExecute(long articleId)
        {
            this.Executed?.Invoke(this, articleId);
        }
    }

    private class Helper : CommandTestHelper<MockArticleCommand>
    {
        protected override MockArticleCommand MakeCommand() => new MockArticleCommand(this.articleDatabase);
    }

    private Helper helper = new Helper();

    public void Dispose()
    {
        this.helper.Dispose();
    }

    private void AssertExecutedNotCalled(object? parameter)
    {
        this.helper.command.Executed += (o, arg) => Assert.Fail("Shouldn't be called");
    }

    private void AssertExecutedCalledWithCorrectParamter(long parameter)
    {
        var wasRaised = false;
        var value = 0L;
        this.helper.command.Executed += (o, a) =>
        {
            wasRaised = true;
            value = a;
        };

        this.helper.command.Execute(parameter);

        Assert.True(wasRaised);
        Assert.Equal(parameter, value);
    }

    [Fact]
    public void CallingRaiseCanExecuteChangedRaisesEvent()
    {
        var wasRaised = false;
        this.helper.command.CanExecuteChanged += (o, a) => wasRaised = true;
        this.helper.command.RaiseCanExecuteChanged();
        Assert.True(wasRaised);
    }

    [Fact]
    public void PassingNullToCanExecuteReturnsFalse()
    {
        Assert.False(this.helper.command.CanExecute(null));
    }

    [Fact]
    public void PassingNonLongToCanExecuteReturnsFalse()
    {
        Assert.False(this.helper.command.CanExecute(String.Empty));
    }

    [Fact]
    public void PassingZeroToCanExecuteReturnsFalse()
    {
        Assert.False(this.helper.command.CanExecute(0L));
    }

    [Fact]
    public void PassingBelowZeroToCanExecuteReturnsFalse()
    {
        Assert.False(this.helper.command.CanExecute(-1L));
    }

    [Fact]
    public void ExecutingCommandWithNullNoListenerSucceeds()
    {
        this.helper.command.Execute(null);
    }

    [Fact]
    public void ExecutingCommandWithNoLongNoListenerSucceeds()
    {
        this.helper.command.Execute(String.Empty);
    }

    [Fact]
    public void ExecutingCommandWithZeroNoListenerSucceeds()
    {
        this.helper.command.Execute(0L);
    }

    [Fact]
    public void ExecutingCommandWithBelowZeroNoListenerSucceeds()
    {
        this.helper.command.Execute(-1L);
    }

    [Fact]
    public void ExecutingCommandWithNullListenerDoesntCallListener()
    {
        this.AssertExecutedNotCalled(null);
    }

    [Fact]
    public void ExecutingCommandWithZeroListenerDoesntCallListener()
    {
        this.AssertExecutedNotCalled(0L);
    }

    [Fact]
    public void ExecutingCommandWithBelowZeroListenerDoesntCallListener()
    {
        this.AssertExecutedNotCalled(-1L);
    }

    [Fact]
    public void ExecutingCommandWithValidArticleIdExecutedWithCorrectParameter()
    {
        this.AssertExecutedCalledWithCorrectParamter(1L);
    }
}