using System.Security.Claims;
using AuctionHub.Data;
using AuctionHub.Models;
using AuctionHub.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize]
public class AuctionsController : Controller
{
    private readonly AuctionHubDbContext _context;

    public AuctionsController(AuctionHubDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int? categoryId)
    {
        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => a.IsActive && a.EndTime > DateTime.Now);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(a => a.Title.Contains(searchTerm) || a.Description.Contains(searchTerm));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        var auctions = await query
            .OrderBy(a => a.EndTime)
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

        ViewBag.Categories = await GetCategoriesAsync();
        ViewBag.CurrentSearch = searchTerm;
        ViewBag.CurrentCategory = categoryId;

        return View(auctions);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var auction = await _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null)
        {
            return NotFound();
        }

        var model = new AuctionDetailsViewModel
        {
            Id = auction.Id,
            Title = auction.Title,
            Description = auction.Description,
            ImageUrl = auction.ImageUrl,
            CurrentPrice = auction.CurrentPrice,
            StartPrice = auction.StartPrice,
            EndTime = auction.EndTime,
            Category = auction.Category.Name,
            Seller = auction.Seller.UserName ?? auction.Seller.Email ?? "Unknown",
            SellerId = auction.SellerId,
            IsActive = auction.IsActive && auction.EndTime > DateTime.Now,
            Bids = auction.Bids
                .OrderByDescending(b => b.BidTime)
                .Select(b => new BidViewModel
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder.UserName ?? b.Bidder.Email ?? "Unknown"
                })
                .ToList(),
            NewBidAmount = auction.CurrentPrice + 1 // Suggest a bid
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> PlaceBid(int auctionId, decimal amount)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Validations
        if (auction.SellerId == currentUserId)
        {
            TempData["Error"] = "You cannot bid on your own auction.";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }

        if (auction.EndTime <= DateTime.Now || !auction.IsActive)
        {
            TempData["Error"] = "This auction has already ended.";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }

        if (amount <= auction.CurrentPrice)
        {
            TempData["Error"] = $"Your bid must be higher than {auction.CurrentPrice:C}.";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }

        // Create the bid
        var bid = new Bid
        {
            AuctionId = auctionId,
            BidderId = currentUserId!,
            Amount = amount,
            BidTime = DateTime.Now
        };

        // Update auction current price
        auction.CurrentPrice = amount;

        _context.Bids.Add(bid);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your bid was placed successfully!";
        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpGet]
    public async Task<IActionResult> MyAuctions()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var auctions = await _context.Auctions
            .Include(a => a.Category)
            .Where(a => a.SellerId == currentUserId)
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

    [HttpGet]
    public async Task<IActionResult> MyBids()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Get auctions where the user has placed at least one bid
        var auctions = await _context.Bids
            .Where(b => b.BidderId == currentUserId)
            .Select(b => b.Auction)
            .Distinct()
            .Include(a => a.Category)
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

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var auction = await _context.Auctions.FindAsync(id);

        if (auction == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId)
        {
            return Forbid();
        }

        var model = new AuctionFormModel
        {
            Title = auction.Title,
            Description = auction.Description,
            ImageUrl = auction.ImageUrl,
            StartPrice = auction.StartPrice,
            EndTime = auction.EndTime,
            CategoryId = auction.CategoryId,
            Categories = await GetCategoriesAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AuctionFormModel model)
    {
        var auction = await _context.Auctions.FindAsync(id);

        if (auction == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        auction.Title = model.Title;
        auction.Description = model.Description;
        auction.ImageUrl = model.ImageUrl;
        auction.EndTime = model.EndTime;
        auction.CategoryId = model.CategoryId;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = auction.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId)
        {
            return Forbid();
        }

        if (auction.Bids.Any())
        {
            TempData["Error"] = "Cannot delete an auction that already has bids.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        _context.Auctions.Remove(auction);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Auction deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new AuctionFormModel
        {
            Categories = await GetCategoriesAsync(),
            EndTime = DateTime.Now.AddDays(7)
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AuctionFormModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var auction = new Auction
        {
            Title = model.Title,
            Description = model.Description,
            ImageUrl = model.ImageUrl,
            StartPrice = model.StartPrice,
            CurrentPrice = model.StartPrice, // Initially same as start price
            EndTime = model.EndTime,
            CreatedOn = DateTime.Now,
            IsActive = true,
            CategoryId = model.CategoryId,
            SellerId = currentUserId
        };

        _context.Auctions.Add(auction);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }
}
