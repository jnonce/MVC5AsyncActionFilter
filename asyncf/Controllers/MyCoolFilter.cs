using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using jnonce.MVC.AsyncActionFilter;

namespace asyncf.Controllers
{
    public class MyCoolFilter : AsyncActionFilterAttribute
    {

        protected override async Task OnRequest(
            AsyncActionFilterAttribute.IRequestContext filterContext)
        {
            Debug.WriteLine("FILTER: Controller has not run yet");

            try
            {
                // Do my first step
                var actionExecutionResults = await filterContext.ExecuteAction();
                Debug.WriteLine("FILTER: Controller completed!");
                if (actionExecutionResults.Result != null)
                {
                    Debug.WriteLine("FILTER: Controller result: {0}", actionExecutionResults.Result);
                }

                //
                await Task.Delay(1000);
                var actionCompletionResults = await filterContext.CompleteActionProcessing();
                Debug.WriteLine("FILTER: Action filters done, now filtering View");

                try
                {
                    //
                    var resultExecutionResults = await filterContext.ExecuteResult();
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