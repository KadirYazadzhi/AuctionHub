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
        return View(settings);
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
