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

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
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
                if (Url.IsLocalUrl(notification.Link))
                {
                    return Redirect(notification.Link);
                }
                // Fallback for absolute internal links if any, otherwise stay safe
                return Redirect(notification.Link); 
            }
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
