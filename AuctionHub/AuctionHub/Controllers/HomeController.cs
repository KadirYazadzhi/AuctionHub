using System.Diagnostics;
using AuctionHub.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using AuctionHub.Domain.Models;
using AuctionHub.Models;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Identity; 

namespace AuctionHub.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IMessageService _messageService;
    private readonly UserManager<ApplicationUser> _userManager; 
    
    public HomeController(
        ILogger<HomeController> logger, 
        IMessageService messageService, 
        UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _messageService = messageService;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }
    
    public async Task<IActionResult> About()
    {
        ViewBag.UserName = "";
        ViewBag.UserEmail = "";

        if (User.Identity?.IsAuthenticated ?? false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.UserEmail = user.Email;
                
                if (!string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.LastName))
                {
                    ViewBag.UserName = $"{user.FirstName} {user.LastName}";
                }
                else
                {
                    ViewBag.UserName = user.UserName;
                }
            }
        }

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

        var contactMessage = new ContactMessageDto
        {
            Name = name,
            Email = email,
            Message = message
        };

        await _messageService.CreateAsync(contactMessage);

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