using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using jnonce.MVC.AsyncActionFilter;

namespace asyncf.Controllers
{
    public class MinimumDurationAttribute : AsyncActionFilterAttribute
    {
        public int Milliseconds { get; set; }

        protected override Task OnRequest(AsyncActionFilterAttribute.IRequestContext filterContext)
        {
            return Task.WhenAll(
                Task.Delay(this.Milliseconds),
                filterContext.ExecuteAction());
        }
    }
}