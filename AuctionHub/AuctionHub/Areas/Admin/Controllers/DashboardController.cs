using System.Security.Claims;
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

    public async Task<IActionResult> Disputes()
    {
        var disputes = await _adminService.GetDisputedAuctionsAsync();
        return View(disputes);
    }

    [HttpPost]
    public async Task<IActionResult> ResolveDispute(int auctionId, string resolution)
    {
        var adminId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (adminId == null) return Challenge();

        var success = await _adminService.ResolveDisputeAsync(auctionId, resolution, adminId);
        if (success) TempData["Success"] = $"Dispute resolved with: {resolution}";
        else TempData["Error"] = "Failed to resolve dispute.";

        return RedirectToAction(nameof(Disputes));
    }
}
