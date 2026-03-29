using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using AuctionHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class AuctionsController : AdminBaseController
{
    private readonly IAuctionHubDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IAdminService _adminService;

    public AuctionsController(IAuctionHubDbContext context, INotificationService notificationService, IAdminService adminService)
    {
        _context = context;
        _notificationService = notificationService;
        _adminService = adminService;
    }

    public async Task<IActionResult> Index()
    {
        var auctions = await _context.Auctions
            .IgnoreQueryFilters() // SHOW EVERYTHING
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .OrderByDescending(a => a.CreatedOn)
            .Select(a => new AuctionListViewModel
            {
                Id = a.Id,
                PublicId = a.PublicId,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category != null ? a.Category.Name : "General",
                City = a.City,
                Country = a.Country,
                IsActive = a.IsActive,
                IsPromoted = a.IsPromoted,
                IsSuspended = a.IsSuspended,
                IsDeleted = a.IsDeleted,
                SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
                SellerId = a.SellerId,
                WinnerId = a.Bids.OrderByDescending(b => b.Amount).Select(b => b.BidderId).FirstOrDefault()
            })
            .ToListAsync();

        return View(auctions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var auction = await _context.Auctions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        // FULL RESTORE: Re-activate and reset flags
        auction.IsDeleted = false;
        auction.IsActive = true;
        auction.IsSuspended = false;
        auction.IsPromoted = false; // Promotion is lost after deletion
        
        // Ensure EndTime is in the future (optional but recommended)
        if (auction.EndTime <= DateTime.UtcNow)
        {
            // If it was already expired, give it 24 hours more to be visible
            auction.EndTime = DateTime.UtcNow.AddDays(1);
        }

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (adminId != null)
        {
            await _adminService.LogActionAsync(adminId, "Restore Auction", "Auction", id.ToString(), "Admin fully restored and reactivated auction");
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Auction restored and reactivated successfully.";
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsuspend(int id)
    {
        var auction = await _context.Auctions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        auction.IsSuspended = false;
        auction.IsActive = true; // Reactive the auction
        
        // Ensure end time is in the future
        if (auction.EndTime <= DateTime.UtcNow)
        {
            auction.EndTime = DateTime.UtcNow.AddDays(1);
        }

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (adminId != null)
        {
            await _adminService.LogActionAsync(adminId, "Unsuspend Auction", "Auction", id.ToString(), "Admin lifted auction suspension");
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Auction unsuspended and reactivated.";
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Suspend Auction
            auction.IsSuspended = true;
            auction.IsActive = false;

            // 2. Refund Highest Bidder if exists AND auction is not settled
            var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (highestBid != null && !auction.IsSettled)
            {
                var bidder = await _context.Users.FindAsync(highestBid.BidderId);
                if (bidder != null)
                {
                    bidder.WalletBalance += highestBid.Amount;
                    
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = bidder.Id,
                        Amount = highestBid.Amount,
                        TransactionType = "AdminRefund",
                        Description = $"Refund for suspended auction: {auction.Title}",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = id
                    });

                    // Notify Bidder
                    await _notificationService.NotifyUserAsync(bidder.Id, 
                        $"⚠️ The auction '{auction.Title}' was suspended by administration. Your bid of {highestBid.Amount:C} has been fully refunded.", 
                        "#");
                }
            }

            // 3. Notify Seller
            await _notificationService.NotifyUserAsync(auction.SellerId, 
                $"⛔ Your auction '{auction.Title}' has been suspended due to a policy violation.", 
                "#");

            // 4. Audit Log
            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (adminId != null)
            {
                await _adminService.LogActionAsync(adminId, "Suspend Auction", "Auction", id.ToString(), "Policy violation suspension");
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Auction suspended and funds refunded.";
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Error suspending auction.";
        }

        return RedirectToAction(nameof(Index));
    }
}

