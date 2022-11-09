using Codevoid.Storyvoid;
using Codevoid.Storyvoid.ViewModels.Commands;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class ArticleCommandTests : IDisposable
{
    private SqliteConnection connection;
    private MockArticleCommand command;

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

    public ArticleCommandTests()
    {
        var (connection, _, _, articles, _) = TestUtilities.GetDatabases();
        this.command = new MockArticleCommand(articles);
        this.connection = connection;
    }

    public void Dispose()
    {
        this.connection?.Close();
        this.connection?.Dispose();
    }

    private void AssertExecutedNotCalled(object? parameter)
    {
        this.command.Executed += (o, arg) => Assert.Fail("Shouldn't be called");
    }

    private void AssertExecutedCalledWithCorrectParamter(long parameter)
    {
        var wasRaised = false;
        var value = 0L;
        this.command.Executed += (o, a) =>
        {
            wasRaised = true;
            value = a;
        };

        this.command.Execute(parameter);

        Assert.True(wasRaised);
        Assert.Equal(parameter, value);
    }

    [Fact]
    public void CallingRaiseCanExecuteChangedRaisesEvent()
    {
        var wasRaised = false;
        this.command.CanExecuteChanged += (o, a) => wasRaised = true;
        this.command.RaiseCanExecuteChanged();
        Assert.True(wasRaised);
    }

    [Fact]
    public void PassingNullToCanExecuteReturnsFalse()
    {
        Assert.False(this.command.CanExecute(null));
    }

    [Fact]
    public void PassingNonLongToCanExecuteReturnsFalse()
    {
        Assert.False(this.command.CanExecute(String.Empty));
    }

    [Fact]
    public void PassingZeroToCanExecuteReturnsFalse()
    {
        Assert.False(this.command.CanExecute(0L));
    }

    [Fact]
    public void PassingBelowZeroToCanExecuteReturnsFalse()
    {
        Assert.False(this.command.CanExecute(-1L));
    }

    [Fact]
    public void ExecutingCommandWithNullNoListenerSucceeds()
    {
        this.command.Execute(null);
    }

    [Fact]
    public void ExecutingCommandWithNoLongNoListenerSucceeds()
    {
        this.command.Execute(String.Empty);
    }

    [Fact]
    public void ExecutingCommandWithZeroNoListenerSucceeds()
    {
        this.command.Execute(0L);
    }

    [Fact]
    public void ExecutingCommandWithBelowZeroNoListenerSucceeds()
    {
        this.command.Execute(-1L);
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