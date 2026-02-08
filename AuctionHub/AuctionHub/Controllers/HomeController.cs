using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AuctionHub.Models;

namespace AuctionHub.Controllers;

public class HomeController : Controller {
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger) {
        _logger = logger;
    }

        public IActionResult Index()

        {

            return View();

        }

    

            public IActionResult About()

    

            {

    

                return View();

    

            }

    

        

    

            public IActionResult HelpCenter()

    

            {

    

                return View();

    

            }

    

        

    

            public IActionResult TrustAndSafety()

    

            {

    

                return View();

    

            }

    

        

    

            public IActionResult SellingTips()

    

            {

    

                return View();

    

            }

    

        

    

            public IActionResult TermsOfService()

    

            {

    

                return View();

    

            }

    

        

    

            public IActionResult Privacy()

        {

            return View();

        }

    

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]

        public IActionResult Error(int? statusCode = null)

        {

            if (statusCode.HasValue && statusCode.Value == 404)

            {

                return View("NotFound");

            }

    

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        }

    }