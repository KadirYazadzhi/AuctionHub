using System.Diagnostics;
using AuctionHub.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using AuctionHub.Domain.Models;
using AuctionHub.Models;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Identity; 

using Microsoft.AspNetCore.Identity.UI.Services;

namespace AuctionHub.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IMessageService _messageService;
    private readonly UserManager<ApplicationUser> _userManager; 
    private readonly IChatService _chatService;
    private readonly IAuctionService _auctionService;
    private readonly IEmailSender _emailSender;
    
    public HomeController(
        ILogger<HomeController> logger, 
        IMessageService messageService, 
        UserManager<ApplicationUser> userManager,
        IChatService chatService,
        IAuctionService auctionService,
        IEmailSender emailSender)
    {
        _logger = logger;
        _messageService = messageService;
        _userManager = userManager;
        _chatService = chatService;
        _auctionService = auctionService;
        _emailSender = emailSender;
    }

    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);
        ViewBag.EndingSoon = await _auctionService.GetEndingSoonAuctionsAsync(4, currentUserId);
        var globalMessages = await _chatService.GetGlobalMessagesAsync(50);
        return View(globalMessages);
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

        // Send Email Notifications
        try 
        {
            // 1. Notify Admin
            await _emailSender.SendEmailAsync("admin@auctionhub.com", $"New Contact Message from {name}", 
                $"<h3>Message from {name} ({email})</h3><p>{message}</p>");

            // 2. Auto-reply to User
            await _emailSender.SendEmailAsync(email, "We've received your message!", 
                $"<h3>Hi {name},</h3><p>Thank you for reaching out to AuctionHub. We have received your message and our team will get back to you shortly.</p><br><p>Best regards,<br>AuctionHub Team</p>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact emails.");
        }

        TempData["Success"] = "Thank you! Your message has been sent. Check your email for confirmation.";
        return RedirectToAction(nameof(About));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Please provide a valid email.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _emailSender.SendEmailAsync(email, "Welcome to AuctionHub Newsletter!", 
                "<h3>Welcome aboard!</h3><p>You've successfully subscribed to our newsletter. We'll keep you updated with the latest rare finds and auctions!</p>");
            
            TempData["Success"] = "Successfully subscribed! Welcome to our community.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send subscription email.");
            TempData["Error"] = "Subscription failed. Please try again later.";
        }

        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue)
        {
            if (statusCode.Value == 404) return View("NotFound");
            if (statusCode.Value == 500) return View("ServerError");
        }

        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}