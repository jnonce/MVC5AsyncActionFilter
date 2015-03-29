using System;
using System.Threading;

namespace asyncf.Controllers
{
    /// <summary>
    /// Disposable object which sets the <see cref="SynchronizationContext"/> on
    /// construction and resets on disposal.
    /// </summary>
    internal sealed class SynchronizationContextLock : IDisposable
    {
        private SynchronizationContext previous;
        private SynchronizationContext current;

        /// <summary>
        /// Initializes a new instance of the <see cref="SynchronizationContextLock"/> class.
        /// </summary>
        /// <param name="nextContext">The context to set as current for the duration of this lock.</param>
        public SynchronizationContextLock(SynchronizationContext nextContext)
        {
            previous = SynchronizationContext.Current;
            current = nextContext;

            if (previous != current)
            {
                SynchronizationContext.SetSynchronizationContext(current);
            }
        }

        /// <summary>
        /// Restore the original sync context.
        /// </summary>
        public void Dispose()
        {
            if (SynchronizationContext.Current == current)
            {
                SynchronizationContext.SetSynchronizationContext(previous);
                current = previous;
            }
        }
    }
}