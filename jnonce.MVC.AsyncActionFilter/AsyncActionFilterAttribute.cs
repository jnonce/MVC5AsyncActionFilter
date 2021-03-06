﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace jnonce.MVC.AsyncActionFilter
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
        /// <param name="sequencer">The sequencer used to coordinate executing the controller and the results.</param>
        /// <returns>A <see cref="Task"/> representing the filter's request processing</returns>
        protected abstract Task OnRequest(IActionSequencer sequencer);


        /// <summary>
        /// Called by the ASP.NET MVC framework before the action method executes.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnActionExecuting(filterContext, OnRequest);
            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// Called by the ASP.NET MVC framework after the action method executes.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnActionExecuted(filterContext);
            base.OnActionExecuted(filterContext);
        }

        /// <summary>
        /// Called by the ASP.NET MVC framework before the action result executes.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var state = GetRequestProcessor(filterContext);
            state.OnResultExecuting(filterContext);
            base.OnResultExecuting(filterContext);
        }

        /// <summary>
        /// Called by the ASP.NET MVC framework after the action result executes.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
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
        private class RequestProcessor : IActionSequencer
        {
            // SyncContext allows us the run async callbacks as a message pump
            private readonly SynchronousSynchronizationContext SyncContext = new SynchronousSynchronizationContext();

            // Monitor used to tell when an execution stage can progress and when new messages are ready to be pumped
            private readonly object @lock = new object();

            // Current state of progress.  Async processing (implicitly) advances this variable which lets us know
            // when it's OK to exit out of a processing function and allow MVC to continue execution.
            private ExecutionProgress progressAllowed = ExecutionProgress.None;

            // Awaitables for the various stages of request processing
            private TaskCompletionSource<ActionExecutedContext> ActionExecuted = new TaskCompletionSource<ActionExecutedContext>();
            private TaskCompletionSource<ResultExecutingContext> ActionCompleted = new TaskCompletionSource<ResultExecutingContext>();
            private TaskCompletionSource<ResultExecutedContext> ResultExecuted = new TaskCompletionSource<ResultExecutedContext>();       

            public RequestProcessor()
            {
                this.SyncContext.ActionQueued += SyncContext_ActionQueued;
            }

            #region ActionFilterAttribute-like methods

            public void OnActionExecuting(ActionExecutingContext filterContext, Func<IActionSequencer, Task> begin)
            {
                this.ActionExecuting = filterContext;

                // Begin running the async operation.  As we'll be blocking execution we'll be
                // sure to run it on the thread pool
                using (SyncContext.Use())
                {
                    begin(this).ContinueWith(task =>
                        {
                            // When the async operation completes signal that all steps are now free to execute.
                            AllowProgress(ExecutionProgress.EndRequest);
                        });

                    // Don't allow the controller to run until either the task has completed or
                    // it has signalled us to move on
                    PumpMessagesUntil(ExecutionProgress.ExecuteAction);
                }
            }

            public void OnActionExecuted(ActionExecutedContext filterContext)
            {
                using (SyncContext.Use())
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

                    PumpMessagesUntil(ExecutionProgress.BeginResultComprehension);
                }
            }

            public void OnResultExecuting(ResultExecutingContext filterContext)
            {
                using (SyncContext.Use())
                {
                    // Resume the async process
                    ActionCompleted.SetResult(filterContext);

                    // Wait for the Task to complete or to request we proceed
                    PumpMessagesUntil(ExecutionProgress.ExecuteResult);
                }
            }

            public void OnResultExecuted(ResultExecutedContext filterContext)
            {
                using (SyncContext.Use())
                {
                    if (filterContext.Exception != null && !filterContext.ExceptionHandled)
                    {
                        ResultExecuted.SetException(new AggregateException(filterContext.Exception));
                    }
                    else
                    {
                        ResultExecuted.SetResult(filterContext);
                    }

                    PumpMessagesUntil(ExecutionProgress.EndRequest);
                }
            }

            #endregion

            #region IActionSequencer implementation

            // The IActionSequencer implementation is used by the derived attribute classes to signal
            // when theyr'e ready for a certain stage of MVC action processing.  These methods
            // mark the progression state to tell our four ActionFilterAttribute-like methods when they
            // can exit (and thereby allow MVC to continue).

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

            #endregion

            // Wait for a callback to indicate that the requested progress is reached
            // Run the message pump until that occurs.
            private void PumpMessagesUntil(ExecutionProgress demandedProgress)
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
            private void SyncContext_ActionQueued()
            {
                lock (@lock)
                {
                    Monitor.Pulse(@lock);
                }
            }
        }
    }
}