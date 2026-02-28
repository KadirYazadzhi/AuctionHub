using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class UsersController : AdminBaseController
{
    private readonly IUserService _userService;
    private readonly IAdminService _adminService;

    public UsersController(IUserService userService, IAdminService adminService)
    {
        _userService = userService;
        _adminService = adminService;
    }

    public async Task<IActionResult> Index(string? searchTerm)
    {
        var users = await _userService.GetAllAsync(searchTerm);
        return View(users);
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _userService.GetByIdAsync(id);

        if (user == null) return NotFound();

        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBalance(string userId, decimal amount, string reason)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _userService.UpdateBalanceAsync(userId, amount, reason);

        if (result.Success)
        {
            if (adminId != null)
            {
                await _adminService.LogActionAsync(adminId, "Update User Balance", "User", userId, $"Amount: {amount:C}. Reason: {reason}");
            }
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLock(string userId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _userService.ToggleLockAsync(userId);

        if (result.Success)
        {
            if (adminId != null)
            {
                await _adminService.LogActionAsync(adminId, "Toggle User Lock", "User", userId, result.Message);
            }
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleShadowBan(string userId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _userService.ToggleShadowBanAsync(userId);

        if (result.Success)
        {
            if (adminId != null)
            {
                await _adminService.LogActionAsync(adminId, "Toggle Shadow-ban", "User", userId, result.Message);
            }
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = userId });
    }
}
