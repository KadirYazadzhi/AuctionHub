using AuctionHub.Data;
using AuctionHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class AuctionsController : AdminBaseController
{
    private readonly AuctionHubDbContext _context;

    public AuctionsController(AuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var auctions = await _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .OrderByDescending(a => a.CreatedOn)
            .Select(a => new AuctionListViewModel
            {
                Id = a.Id,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category.Name,
                IsActive = a.IsActive
            })
            .ToListAsync();

        return View(auctions);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var auction = await _context.Auctions.FindAsync(id);
        if (auction == null) return NotFound();

        // Admin can delete even if there are bids
        _context.Auctions.Remove(auction);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Auction deleted successfully by Admin.";
        return RedirectToAction(nameof(Index));
    }
}
