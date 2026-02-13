using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize]
public class WalletController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWalletService _walletService;

    public WalletController(UserManager<ApplicationUser> userManager, IWalletService walletService)
    {
        _userManager = userManager;
        _walletService = walletService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var transactions = await _walletService.GetTransactionsAsync(user.Id);
        ViewBag.Transactions = transactions;

        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> AddFunds(decimal amount)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _walletService.AddFundsAsync(currentUserId, amount);

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