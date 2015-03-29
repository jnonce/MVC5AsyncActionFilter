using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace asyncf.Controllers
{
    [MyCoolFilter]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            Debug.WriteLine("HomeController executing!");
            return View();
        }

        public ActionResult Fail()
        {
            throw new Exception("YOU DIED");
        }

        [MinimumDuration(Milliseconds = 3000)]
        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}