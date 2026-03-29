using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly IAuctionHubDbContext _context;

    public NotificationsController(INotificationService notificationService, IAuctionHubDbContext context)
    {
        _notificationService = notificationService;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId);

        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> RedirectToLink(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId);
        var notification = notifications.FirstOrDefault(n => n.Id == id);

        if (notification != null)
        {
            await _notificationService.MarkAsReadAsync(id, userId);
            if (!string.IsNullOrEmpty(notification.Link) && notification.Link != "#")
            {
                var link = notification.Link;
                if (!link.StartsWith("/") && !link.StartsWith("http"))
                {
                    link = "/" + link;
                }

                // INTELLIGENT FALLBACK: Fix old integer-based links /Auctions/Details/123
                if (link.Contains("/Auctions/Details/"))
                {
                    var parts = link.Split('/');
                    var lastPart = parts.Last();
                    if (int.TryParse(lastPart, out int oldId))
                    {
                        var auction = await _context.Auctions.FindAsync(oldId);
                        if (auction != null)
                        {
                            link = $"/Auctions/Details/{auction.PublicId}";
                        }
                    }
                }
                
                return Redirect(link); 
            }
            TempData["Error"] = "Notification link is empty or invalid.";
        }
        else
        {
            TempData["Error"] = $"Notification with ID {id} not found.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _notificationService.MarkAsReadAsync(id, userId!);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _notificationService.MarkAllAsReadAsync(userId!);
        return RedirectToAction(nameof(Index));
    }
    
    // API endpoint for the bell icon badge
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Ok(0);
        
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }
}
