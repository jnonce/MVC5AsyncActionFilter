using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace asyncf.Controllers
{
    public class AsyncViewDataAttribute : AsyncActionFilterAttribute
    {
        private HttpClient client;

        public AsyncViewDataAttribute(string domain)
        {
            client = new HttpClient();
            client.BaseAddress = new Uri(domain, UriKind.Absolute);
        }

        protected override async Task OnRequest(AsyncActionFilterAttribute.IRequestContext filterContext)
        {
            // Allow the controller to run
            ActionExecutedContext actionResult = await filterContext.ExecuteAction();

            // Begin loading data
            if (actionResult.Result is ViewResult && actionResult.Exception == null)
            {
                // Begin retrieving data
                Task<HttpResponseMessage> resp = client.GetAsync("/");

                // Wait for view processing to begin
                ResultExecutingContext resultExecutingContext = await filterContext.CompleteActionProcessing();
                var result = resultExecutingContext.Result as ViewResult;

                // Get the view result
                if (result != null)
                {
                    HttpResponseMessage msg = await resp;
                    result.ViewBag.Field = await msg.Content.ReadAsStringAsync();
                }
            }
        }
    }
}