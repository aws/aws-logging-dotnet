using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebSample.Controllers
{
    public class HomeController : Controller
    {
        ILogger<HomeController> Logger { get; set; }

        public HomeController(ILogger<HomeController> logger)
        {
            this.Logger = logger;
        }

        public IActionResult Index()
        {
            Logger.LogInformation("Welcome to the AWS Logger. You are viewing the home page");
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
