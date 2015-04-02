using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using jnonce.MVC.AsyncActionFilter.Application;

[assembly: ApplicationLifecycle(typeof(asyncf.App_Start.CoolStart), "Run1", 1)]
[assembly: ApplicationLifecycle(typeof(asyncf.App_Start.CoolStart), "Run2", 2)]

namespace asyncf.App_Start
{
    /// <summary>
    /// Test cases for app start and stop markers
    /// </summary>
    public static class CoolStart
    {
        public static async Task Run1(Func<Task> next)
        {
            Debug.WriteLine("STARTUP1: Initial");
            await Task.Delay(3000);

            Debug.WriteLine("STARTUP1: Initial, post delay");
            await next();

            Debug.WriteLine("STARTUP1: Final");
            await Task.Delay(3000);
            Debug.WriteLine("STARTUP1: Final, post delay");
        }

        public static async Task Run2(Func<Task> next)
        {
            Debug.WriteLine("STARTUP2: Initial");
            await next();
            Debug.WriteLine("STARTUP2: Final");
        }
    }
}