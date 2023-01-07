using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Base class to encapsulate raising events on a UI dispatcher
/// </summary>
internal class EventDispatcherBase
{
    private readonly DispatcherQueue queue;

    /// <summary>
    /// Instantiate w/ the supplied dispatcher
    /// </summary>
    /// <param name="targetDispatcherQueue"></param>
    protected EventDispatcherBase(DispatcherQueue targetDispatcherQueue)
    {
        this.queue = targetDispatcherQueue;
    }

    /// <summary>
    /// Invokes the supplied operation on the dispatcher. If we're already on
    /// the dispatcher, we will execute it directly. Otherwise we'll queue it on
    /// our dispatcher queue for later execution.
    /// </summary>
    /// <param name="operation">Encapsulated operation to perform</param>
    protected void PerformOnDispatcher(DispatcherQueueHandler operation)
    {
        if (this.queue.HasThreadAccess)
        {
            // If we're already on the right thread, we can just invoke the
            // operation directly.
            operation();
            return;
        }

        this.queue.TryEnqueue(operation);
    }
}
