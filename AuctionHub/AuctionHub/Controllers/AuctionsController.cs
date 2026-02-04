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
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AuctionsController(AuctionHubDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int? categoryId)
    {
        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var normalizedSearch = searchTerm.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(normalizedSearch) || 
                             a.Description.ToLower().Contains(normalizedSearch));
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
            MinIncrease = auction.MinIncrease,
            EndTime = auction.EndTime,
            Category = auction.Category.Name,
            Seller = auction.Seller.UserName ?? auction.Seller.Email ?? "Unknown",
            SellerId = auction.SellerId,
            IsActive = auction.IsActive && auction.EndTime > DateTime.UtcNow,
            Bids = auction.Bids
                .OrderByDescending(b => b.BidTime)
                .Select(b => new BidViewModel
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder.UserName ?? b.Bidder.Email ?? "Unknown"
                })
                .ToList(),
            NewBidAmount = auction.CurrentPrice + auction.MinIncrease
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> PlaceBid(int auctionId, decimal amount)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var auction = await _context.Auctions
                .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
                .FirstOrDefaultAsync(a => a.Id == auctionId);

            if (auction == null) return NotFound();

            if (auction.SellerId == currentUserId)
            {
                TempData["Error"] = "You cannot bid on your own auction.";
                return RedirectToAction(nameof(Details), new { id = auctionId });
            }

            if (auction.EndTime <= DateTime.UtcNow || !auction.IsActive)
            {
                TempData["Error"] = "This auction has already ended.";
                return RedirectToAction(nameof(Details), new { id = auctionId });
            }

            if (amount < auction.CurrentPrice + auction.MinIncrease)
            {
                TempData["Error"] = $"Your bid must be at least {auction.CurrentPrice + auction.MinIncrease:C}.";
                return RedirectToAction(nameof(Details), new { id = auctionId });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || currentUser.WalletBalance < amount)
            {
                TempData["Error"] = "Insufficient funds in your wallet.";
                return RedirectToAction(nameof(Details), new { id = auctionId });
            }

            currentUser.WalletBalance -= amount;

            var previousHighBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (previousHighBid != null)
            {
                if (previousHighBid.BidderId == currentUserId)
                {
                    currentUser.WalletBalance += previousHighBid.Amount;
                }
                else
                {
                    var previousBidder = previousHighBid.Bidder; 
                    previousBidder.WalletBalance += previousHighBid.Amount;
                }
            }

            var bid = new Bid
            {
                AuctionId = auctionId,
                BidderId = currentUserId,
                Amount = amount,
                BidTime = DateTime.UtcNow
            };

            auction.CurrentPrice = amount;
            auction.Bids.Add(bid);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Your bid was placed successfully!";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Someone else placed a bid just now. Please review the new price and try again.";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "An error occurred while placing your bid. Please try again.";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }
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
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId) return Forbid();

        if (auction.Bids.Any())
        {
            TempData["Error"] = "You cannot edit an auction that has existing bids.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        var model = new AuctionFormModel
        {
            Title = auction.Title,
            Description = auction.Description,
            ImageUrl = auction.ImageUrl,
            StartPrice = auction.StartPrice,
            MinIncrease = auction.MinIncrease,
            EndTime = auction.EndTime,
            CategoryId = auction.CategoryId,
            Categories = await GetCategoriesAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AuctionFormModel model)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId) return Forbid();

        if (auction.Bids.Any())
        {
             TempData["Error"] = "You cannot edit an auction that has existing bids.";
             return RedirectToAction(nameof(Details), new { id = id });
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        string? imagePath = model.ImageUrl;
        if (model.ImageFile != null)
        {
            imagePath = await SaveImageAsync(model.ImageFile);
        }

        auction.Title = model.Title;
        auction.Description = model.Description;
        if (!string.IsNullOrEmpty(imagePath)) 
        {
            auction.ImageUrl = imagePath;
        }
        
        auction.StartPrice = model.StartPrice;
        auction.MinIncrease = model.MinIncrease;
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

        if (auction == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (auction.SellerId != currentUserId) return Forbid();

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
            EndTime = DateTime.UtcNow.AddDays(7),
            StartPrice = 10.00m,
            MinIncrease = 1.00m
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AuctionFormModel model)
    {
        string? imagePath = model.ImageUrl;
        if (model.ImageFile != null)
        {
            imagePath = await SaveImageAsync(model.ImageFile);
        }

        if (string.IsNullOrEmpty(imagePath))
        {
             ModelState.AddModelError("ImageUrl", "Please provide either an Image URL or upload a file.");
        }

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
            ImageUrl = imagePath!,
            StartPrice = model.StartPrice,
            CurrentPrice = model.StartPrice,
            MinIncrease = model.MinIncrease,
            EndTime = model.EndTime,
            CreatedOn = DateTime.UtcNow,
            IsActive = true,
            CategoryId = model.CategoryId,
            SellerId = currentUserId!
        };

        _context.Auctions.Add(auction);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "auctions");
        Directory.CreateDirectory(uploadsFolder);
        
        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
        
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }
        
        return "/images/auctions/" + uniqueFileName;
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