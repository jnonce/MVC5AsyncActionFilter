using System;
using System.Threading;

namespace jnonce.MVC.AsyncActionFilter
{
    /// <summary>
    /// <see cref="SynchronizationContext"/> which requires manual message pumping.
    /// </summary>
    public class SynchronousSynchronizationContext : SynchronizationContext
    {
        private object @lock = new object();
        private Action action;

        /// <summary>
        /// Occurs when an action is queued.
        /// </summary>
        public event Action ActionQueued;

        /// <summary>
        /// When overridden in a derived class, dispatches an asynchronous message to a synchronization context.
        /// </summary>
        /// <param name="d">The <see cref="T:System.Threading.SendOrPostCallback" /> delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback d, object state)
        {
            lock (@lock)
            {
                Action previousAction = action;

                action = (previousAction == null)
                    ? new Action(() => d(state))
                    : new Action(() =>
                    {
                        previousAction();
                        d(state);
                    });
            }

            OnActionQueued();
        }

        /// <summary>
        /// When overridden in a derived class, dispatches a synchronous message to a synchronization context.
        /// </summary>
        /// <param name="d">The <see cref="T:System.Threading.SendOrPostCallback" /> delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Send(SendOrPostCallback d, object state)
        {
            Action act;

            if (TryGetAction(out act))
            {
                act();
            }

            d(state);
        }

        /// <summary>
        /// Attempts to get the queued action.
        /// </summary>
        /// <param name="action">The action retrieved.</param>
        /// <returns>
        /// True if an action was extracted, otherwise false.
        /// </returns>
        public bool TryGetAction(out Action action)
        {
            lock (@lock)
            {
                if (this.action == null)
                {
                    action = null;
                    return false;
                }
                else
                {
                    action = this.action;
                    this.action = null;
                    return true;
                }
            }
        }

        private void OnActionQueued()
        {
            var evnt = ActionQueued;
            if (evnt != null)
            {
                evnt();
            }
        }
    }
}