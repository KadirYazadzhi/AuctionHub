using System.Security.Claims;
using AuctionHub.Data;
using AuctionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Controllers;

[Authorize]
public class WalletController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuctionHubDbContext _context;

    public WalletController(UserManager<ApplicationUser> userManager, AuctionHubDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        return View(user); // Pass user model to view to show balance
    }

    [HttpPost]
    public async Task<IActionResult> AddFunds(decimal amount)
    {
        if (amount <= 0)
        {
            TempData["Error"] = "Please enter a valid amount greater than 0.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Update balance
        user.WalletBalance += amount;
        
        await _userManager.UpdateAsync(user);

        TempData["Success"] = $"Successfully added {amount:C} to your wallet!";
        return RedirectToAction(nameof(Index));
    }
}
