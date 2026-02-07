using System.Security.Claims;
using AuctionHub.Data;
using AuctionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // Load transactions
        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        ViewBag.Transactions = transactions;

        return View(user);
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
        
        // Log transaction
        var transaction = new Transaction
        {
            UserId = user.Id,
            Amount = amount,
            Description = "Deposit funds",
            TransactionType = "Deposit",
            TransactionDate = DateTime.UtcNow
        };
        _context.Transactions.Add(transaction);
        
        await _userManager.UpdateAsync(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Successfully added {amount:C} to your wallet!";
        return RedirectToAction(nameof(Index));
    }
}