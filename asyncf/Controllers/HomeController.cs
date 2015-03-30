using System;
using System.Web.Mvc;
using asyncf.Models;

namespace asyncf.Controllers
{
    [MyCoolFilter]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            this.HttpContext.Trace.Write("Controller", "executing");

            return View(GetScenarios());
        }

        public ActionResult Fail()
        {
            throw new Exception("YOU DIED");
        }

        [MinimumDuration(Milliseconds = 3000)]
        public ActionResult Delayed()
        {
            return View();
        }

        public ActionResult NotFound()
        {
            return this.HttpNotFound();
        }

        private ScenarioTarget[] GetScenarios()
        {
            return new[]
                {
                    new ScenarioTarget
                    {
                        Target = this.Url.Action("Index"),
                        Title = "Default",
                        Description = "Default, initial action.  That's this page."
                    },
                    new ScenarioTarget
                    {
                        Target = this.Url.Action("Delayed"),
                        Title = "Delayed",
                        Description = "Filter which forces a min processing delay."
                    },
                    new ScenarioTarget
                    {
                        Target = this.Url.Action("Fail"),
                        Title = "Fail",
                        Description = "Action which fails"
                    },
                    new ScenarioTarget
                    {
                        Target = this.Url.Action("NotFound"),
                        Title = "NotFound",
                        Description = "Results in a 404"
                    },
                };
        }
    }
}