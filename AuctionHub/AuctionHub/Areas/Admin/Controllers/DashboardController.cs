using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class DashboardController : AdminBaseController
{
    private readonly IAuctionHubDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IAdminService _adminService;

    public DashboardController(IAuctionHubDbContext context, INotificationService notificationService, IAdminService adminService)
    {
        _context = context;
        _notificationService = notificationService;
        _adminService = adminService;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _adminService.GetDashboardStatsAsync();
        var suspiciousActivities = await _adminService.GetSuspiciousActivitiesAsync();
        
        // Also fetch audit logs for the dashboard preview
        var recentLogs = await _context.AuditLogs
            .Include(l => l.Admin)
            .OrderByDescending(l => l.Timestamp)
            .Take(5)
            .ToListAsync();

        ViewBag.Stats = stats;
        ViewBag.RecentLogs = recentLogs;
        ViewBag.SuspiciousActivities = suspiciousActivities;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendAnnouncement(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Message cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        await _notificationService.NotifyAllUsersAsync($"📢 SYSTEM: {message}");
        TempData["Success"] = "Announcement sent to all users.";
        return RedirectToAction(nameof(Index));
    }
}
