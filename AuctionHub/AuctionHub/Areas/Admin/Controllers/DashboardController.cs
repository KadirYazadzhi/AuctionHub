using AuctionHub.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class DashboardController : AdminBaseController
{
    private readonly AuctionHubDbContext _context;

    public DashboardController(AuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalAuctions = await _context.Auctions.CountAsync();
        var activeAuctions = await _context.Auctions.CountAsync(a => a.IsActive && a.EndTime > DateTime.UtcNow);
        var totalBids = await _context.Bids.CountAsync();
        var totalWalletBalance = await _context.Users.SumAsync(u => u.WalletBalance);

        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalAuctions = totalAuctions;
        ViewBag.ActiveAuctions = activeAuctions;
        ViewBag.TotalBids = totalBids;
        ViewBag.TotalWalletBalance = totalWalletBalance;

        return View();
    }
}
