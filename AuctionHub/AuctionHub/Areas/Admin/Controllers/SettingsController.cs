using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Areas.Admin.Controllers;

public class SettingsController : AdminBaseController
{
    private readonly IAdminService _adminService;

    public SettingsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _adminService.GetSystemSettingsAsync();
        ViewBag.IsMaintenanceMode = await _adminService.IsMaintenanceModeEnabledAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearCache()
    {
        await _adminService.ClearCacheAsync();
        TempData["Success"] = "System cache cleared successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMaintenance()
    {
        var result = await _adminService.ToggleMaintenanceModeAsync();
        if (result.Enabled) TempData["Success"] = result.Message;
        else TempData["Info"] = result.Message;
        
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport()
    {
        var fileBytes = await _adminService.ExportTransactionsToCsvAsync();
        return File(fileBytes, "text/csv", $"AuctionHub_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpPost]
    public async Task<IActionResult> Update(string key, string value)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminId == null) return Challenge();

        var success = await _adminService.UpdateSystemSettingAsync(key, value, adminId);
        
        if (success)
        {
            TempData["Success"] = $"Setting '{key}' updated successfully.";
        }
        else
        {
            TempData["Error"] = $"Failed to update setting '{key}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AuditLogs(int pageNumber = 1)
    {
        var logs = await _adminService.GetAuditLogsAsync(pageNumber, 20);
        return View(logs);
    }
}
