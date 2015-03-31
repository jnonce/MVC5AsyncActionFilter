using System;
using System.Threading;

namespace jnonce.MVC.AsyncActionFilter
{
    /// <summary>
    /// Disposable object which sets the <see cref="SynchronizationContext"/> on
    /// construction and resets on disposal.
    /// </summary>
    internal struct SynchronizationContextLock : IDisposable
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

            SynchronizationContext.SetSynchronizationContext(current);
        }

        /// <summary>
        /// Restore the original sync context.
        /// </summary>
        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    internal static class SynchronizationContextExtensions
    {
        public static SynchronizationContextLock Use(this SynchronizationContext context)
        {
            return new SynchronizationContextLock(context);
        }
    }
}