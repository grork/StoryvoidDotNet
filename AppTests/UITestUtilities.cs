using Microsoft.UI.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Runtime.CompilerServices;

namespace Codevoid.Test.Storyvoid;

// Lifted from https://github.com/microsoft/Windows-task-snippets/blob/master/tasks/Thread-switching-within-a-task.md
// See also https://devblogs.microsoft.com/pfxteam/await-anything/ for context
// on the type shape
struct DispatcherQueueThreadSwitcher : INotifyCompletion
{
    private DispatcherQueue dispatcher;

    internal DispatcherQueueThreadSwitcher(DispatcherQueue dispatcher) =>
        this.dispatcher = dispatcher;

    public DispatcherQueueThreadSwitcher GetAwaiter() => this;

    public bool IsCompleted => dispatcher.HasThreadAccess;

    public bool GetResult() => dispatcher.HasThreadAccess;

    public void OnCompleted(Action continuation)
    {
        if (!dispatcher.TryEnqueue(() => continuation()))
        {
            continuation();
        }
    }

    public static DispatcherQueueThreadSwitcher SwitchToDispatcher()
    {
        return new DispatcherQueueThreadSwitcher(UITestMethodAttribute.DispatcherQueue);
    }
}