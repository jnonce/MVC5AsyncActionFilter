using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web;
using jnonce.MVC.AsyncActionFilter;

namespace asyncf.Controllers
{
    public class MyCoolFilter : AsyncActionFilterAttribute
    {

        protected override async Task OnRequest(IActionSequencer sequencer)
        {
            TraceContext trace = sequencer.ActionExecuting.HttpContext.Trace;

            trace.Write("FILTER", "Controller has not run yet");

            try
            {
                // Do my first step
                var actionExecutionResults = await sequencer.ExecuteAction();
                trace.Write("FILTER", "Controller completed!");
                if (actionExecutionResults.Result != null)
                {
                    trace.Write(
                        "FILTER", 
                        String.Format("Controller result: {0}", actionExecutionResults.Result)
                        );
                }

                //
                await Task.Delay(1000);
                var actionCompletionResults = await sequencer.CompleteActionProcessing();
                trace.Write("FILTER", "Action filters done, now filtering View");

                try
                {
                    //
                    var resultExecutionResults = await sequencer.ExecuteResult();
                    trace.Write("FILTER", "View is complete!");

                }
                catch (Exception)
                {
                    trace.Write("FILTER", "Error during view engine!");
                }
            }
            catch (Exception)
            {
                trace.Write("FILTER", "Error during rendering!");
            }
        }
    }
}