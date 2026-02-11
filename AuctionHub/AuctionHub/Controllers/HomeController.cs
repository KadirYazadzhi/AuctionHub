using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AuctionHub.Models;
using AuctionHub.Data;

namespace AuctionHub.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AuctionHubDbContext _context;

    public HomeController(ILogger<HomeController> logger, AuctionHubDbContext context)
    {
        _logger = logger;
        _context = context;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string name, string email, string message)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Please fill in all fields.";
            return RedirectToAction(nameof(About));
        }

        var contactMessage = new ContactMessage
        {
            Name = name,
            Email = email,
            Message = message,
            SentOn = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contactMessage);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Thank you! Your message has been sent to our team.";
        return RedirectToAction(nameof(About));
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
