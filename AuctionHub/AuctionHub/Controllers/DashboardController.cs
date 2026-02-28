using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IUserService _userService;
    private readonly IAuctionService _auctionService;

    public DashboardController(IUserService userService, IAuctionService auctionService)
    {
        _userService = userService;
        _auctionService = auctionService;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Administrator"))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        var userDetails = await _userService.GetByIdAsync(userId);
        if (userDetails == null) return NotFound();

        // Get ending soon auctions specifically for this user's interests (optional bonus)
        ViewBag.EndingSoon = await _auctionService.GetEndingSoonAuctionsAsync(3, userId);

        return View(userDetails);
    }
}
