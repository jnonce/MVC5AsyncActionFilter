using System.Threading.Tasks;
using System.Web.Mvc;

namespace jnonce.MVC.AsyncActionFilter
{
    /// <summary>
    /// Sequences the execution of the controller action and result processing.
    /// </summary>
    public interface IActionSequencer
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
