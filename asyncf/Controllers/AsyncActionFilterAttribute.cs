using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace asyncf.Controllers
{
    /// <summary>
    /// An async implementation of an action filter
    /// </summary>
    public abstract class AsyncActionFilterAttribute : ActionFilterAttribute
    {
        private const string StateKey = "AsyncActionFilterAttribute_State_{0}";
        private static int stateKeyIndex;

        private string stateKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncActionFilterAttribute"/> class.
        /// </summary>
        public AsyncActionFilterAttribute()
        {
            this.stateKey = String.Format(StateKey, Interlocked.Increment(ref stateKeyIndex));
        }

        /// <summary>
        /// Called when a controller request is initiated
        /// </summary>
        /// <returns></returns>
        protected abstract Task OnRequest(
            IRequestContext filterContext
            );


        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnActionExecuting(filterContext, OnRequest);
            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnActionExecuted(filterContext);
            base.OnActionExecuted(filterContext);
        }

        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnResultExecuting(filterContext);
            base.OnResultExecuting(filterContext);
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnResultExecuted(filterContext);
            base.OnResultExecuted(filterContext);
        }

        private RequestProcessor GetRequestProcessor(ControllerContext context)
        {
            var state = context.HttpContext.Items[stateKey] as RequestProcessor;
            if (state == null)
            {
                state = new RequestProcessor();
                context.HttpContext.Items[stateKey] = state;
            }
            return state;
        }

        /// <summary>
        /// Represents the progression through action execution and
        /// result computation.
        /// </summary>
        private enum ExecutionProgress
        {
            /// <summary>
            /// No progress has been made
            /// </summary>
            None = 0,

            /// <summary>
            /// The execution of the controller action.
            /// </summary>
            ExecuteAction,

            /// <summary>
            /// The completion of all action filters
            /// </summary>
            BeginResultComprehension,

            /// <summary>
            /// The execution of the result (view)
            /// </summary>
            ExecuteResult,

            /// <summary>
            /// The end of the request
            /// </summary>
            EndRequest
        }

        /// <summary>
        /// Stateful processor for a single request
        /// </summary>
        private class RequestProcessor : IRequestContext
        {
            private readonly SynchronousSynchronizationContext SyncContext = new SynchronousSynchronizationContext();
            private readonly object @lock = new object();

            private Task RequestExecutionTask;

            private ExecutionProgress progressAllowed = ExecutionProgress.None;

            private TaskCompletionSource<ActionExecutedContext> ActionExecuted = new TaskCompletionSource<ActionExecutedContext>();
            private TaskCompletionSource<ResultExecutingContext> ActionCompleted = new TaskCompletionSource<ResultExecutingContext>();
            private TaskCompletionSource<ResultExecutedContext> ResultExecuted = new TaskCompletionSource<ResultExecutedContext>();
       

            public RequestProcessor()
            {
                this.SyncContext.ActionQueued += SyncContext_ActionQueued;
            }

            public void OnActionExecuting(ActionExecutingContext filterContext, Func<IRequestContext, Task> begin)
            {
                this.ActionExecuting = filterContext;

                // Begin running the async operation.  As we'll be blocking execution we'll be
                // sure to run it on the thread pool
                using (LockContext())
                {
                    RequestExecutionTask = begin(this).ContinueWith(task =>
                        {
                            // When the async operation completes signal that all steps are now free to execute.
                            AllowProgress(ExecutionProgress.EndRequest);
                        });

                    // Don't allow the controller to run until either the task has completed or
                    // it has signalled us to move on
                    Wait(ExecutionProgress.ExecuteAction);
                }
            }

            public void OnActionExecuted(ActionExecutedContext filterContext)
            {
                using (LockContext())
                {
                    // Resume the async process
                    if (filterContext.Exception != null && !filterContext.ExceptionHandled)
                    {
                        // If the controller encountered an exception then we propigate that over to
                        // the Task
                        ActionExecuted.SetException(new AggregateException(filterContext.Exception));
                    }
                    else
                    {
                        // Signal that the action has executed and that we have the results
                        ActionExecuted.SetResult(filterContext);

                        // Wait for the Task to complete or for it to signal us to progress
                    }

                    Wait(ExecutionProgress.BeginResultComprehension);
                }
            }

            public void OnResultExecuting(ResultExecutingContext filterContext)
            {
                using (LockContext())
                {
                    // Resume the async process
                    ActionCompleted.SetResult(filterContext);

                    // Wait for the Task to complete or to request we proceed
                    Wait(ExecutionProgress.ExecuteResult);
                }
            }

            public void OnResultExecuted(ResultExecutedContext filterContext)
            {
                using (LockContext())
                {
                    if (filterContext.Exception != null && !filterContext.ExceptionHandled)
                    {
                        ResultExecuted.SetException(new AggregateException(filterContext.Exception));
                    }
                    else
                    {
                        ResultExecuted.SetResult(filterContext);
                    }

                    Wait(ExecutionProgress.EndRequest);
                }
            }

            public ActionExecutingContext ActionExecuting
            {
                get;
                private set;
            }

            public Task<ActionExecutedContext> ExecuteAction()
            {
                AllowProgress(ExecutionProgress.ExecuteAction);
                return ActionExecuted.Task;
            }

            public Task<ResultExecutingContext> CompleteActionProcessing()
            {
                AllowProgress(ExecutionProgress.BeginResultComprehension);
                return ActionCompleted.Task;
            }

            public Task<ResultExecutedContext> ExecuteResult()
            {
                AllowProgress(ExecutionProgress.ExecuteResult);
                return ResultExecuted.Task;
            }

            private IDisposable LockContext()
            {
                return new SynchronizationContextLock(this.SyncContext);
            }

            // Wait for a callback to indicate that the requested progress is reached
            // Run the message pump until that occurs.
            private void Wait(ExecutionProgress demandedProgress)
            {
                while (true)
                {
                    Action action;

                    lock (@lock)
                    {
                        while (true)
                        {
                            if (demandedProgress <= this.progressAllowed)
                            {
                                return;
                            }

                            if (SyncContext.TryGetAction(out action))
                            {
                                break;
                            }

                            Monitor.Wait(@lock);
                        }
                    }

                    action();
                }
            }

            // Mark that a certain stage in execution has been reached, potentially allowing a Wait to complete
            private void AllowProgress(ExecutionProgress progress)
            {
                lock (@lock)
                {
                    if (progress > progressAllowed)
                    {
                        progressAllowed = progress;
                        Monitor.Pulse(@lock);
                    }
                }
            }

            // When an action is queued wake the message pump
            private void SyncContext_ActionQueued(object sender, EventArgs e)
            {
                lock (@lock)
                {
                    Monitor.Pulse(@lock);
                }
            }
        }

        /// <summary>
        /// Context for a request being filtered
        /// </summary>
        public interface IRequestContext
        {
            /// <summary>
            /// Gets the context information for the action executing.
            /// </summary>
            ActionExecutingContext ActionExecuting { get; }

            /// <summary>
            /// Executes the action.  Typically this implies invoking the controller.
            /// </summary>
            /// <returns>Context obtained as a result of executing the action</returns>
            Task<ActionExecutedContext> ExecuteAction();

            /// <summary>
            /// Completes action processing, and ensures that all action filters have run
            /// </summary>
            /// <returns>Context obtained as a result of the completion</returns>
            Task<ResultExecutingContext> CompleteActionProcessing();

            /// <summary>
            /// Executes the result (view).
            /// </summary>
            /// <returns>Context obtained as a result of the completion.</returns>
            Task<ResultExecutedContext> ExecuteResult();
        }        
    }
}