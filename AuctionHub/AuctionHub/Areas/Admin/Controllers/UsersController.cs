using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class UsersController : AdminBaseController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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
        var result = await _userService.UpdateBalanceAsync(userId, amount, reason);

        if (result.Success)
        {
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
        var result = await _userService.ToggleLockAsync(userId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
