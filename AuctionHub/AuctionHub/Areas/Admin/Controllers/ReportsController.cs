using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Areas.Admin.Controllers;

public class ReportsController : AdminBaseController
{
    private readonly IAdminService _adminService;

    public ReportsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<IActionResult> Index(int pageNumber = 1, bool includeResolved = false)
    {
        var reports = await _adminService.GetUserReportsAsync(pageNumber, 20, includeResolved);
        ViewBag.IncludeResolved = includeResolved;
        return View(reports);
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(int reportId, string adminNotes)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminId == null) return Challenge();

        var success = await _adminService.ResolveReportAsync(reportId, adminNotes, adminId);
        
        if (success)
        {
            TempData["Success"] = "Report marked as resolved.";
        }
        else
        {
            TempData["Error"] = "Failed to resolve report.";
        }

        return RedirectToAction(nameof(Index));
    }
}
