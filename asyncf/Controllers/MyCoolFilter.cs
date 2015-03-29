using System;
using System.Diagnostics;
using System.Threading.Tasks;
using jnonce.MVC.AsyncActionFilter;

namespace asyncf.Controllers
{
    public class MyCoolFilter : AsyncActionFilterAttribute
    {

        protected override async Task OnRequest(IActionSequencer sequencer)
        {
            Debug.WriteLine("FILTER: Controller has not run yet");

            try
            {
                // Do my first step
                var actionExecutionResults = await sequencer.ExecuteAction();
                Debug.WriteLine("FILTER: Controller completed!");
                if (actionExecutionResults.Result != null)
                {
                    Debug.WriteLine("FILTER: Controller result: {0}", actionExecutionResults.Result);
                }

                //
                await Task.Delay(1000);
                var actionCompletionResults = await sequencer.CompleteActionProcessing();
                Debug.WriteLine("FILTER: Action filters done, now filtering View");

                try
                {
                    //
                    var resultExecutionResults = await sequencer.ExecuteResult();
                    Debug.WriteLine("FILTER: View is complete!");

                }
                catch (Exception)
                {
                    Debug.WriteLine("FILTER: Error during view engine!");
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("FILTER: Error during rendering!");
            }
        }
    }
}