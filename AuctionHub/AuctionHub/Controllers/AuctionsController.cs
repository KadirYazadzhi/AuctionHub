using System.Security.Claims;
using AuctionHub.Data;
using AuctionHub.Models;
using AuctionHub.Models.ViewModels;
using AuctionHub.Services;
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
    private readonly IAuctionService _auctionService;

    public AuctionsController(AuctionHubDbContext context, IWebHostEnvironment webHostEnvironment, IAuctionService auctionService)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
        _auctionService = auctionService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;
        
        // Get Admin IDs to exclude their test auctions from public view
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => !adminIds.Contains(a.SellerId)) // Hide Admin auctions
            .AsQueryable();

        // Status Filtering
        if (string.IsNullOrEmpty(status) || status == "active")
        {
            query = query.Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);
        }
        else if (status == "closed")
        {
            query = query.Where(a => !a.IsActive || a.EndTime <= DateTime.UtcNow);
        }
        // "all" shows everything

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

        // Price Filtering
        if (minPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice <= maxPrice.Value);
        }

        // Sorting
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            "newest" => query.OrderByDescending(a => a.CreatedOn),
            _ => query.OrderBy(a => a.EndTime) // Default: Ending soonest
        };

        var projectedQuery = query.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive
        });

        int pageSize = 9;
        var paginatedAuctions = await PaginatedList<AuctionListViewModel>.CreateAsync(projectedQuery, pageNumber ?? 1, pageSize);

        ViewBag.Categories = await GetCategoriesAsync(); 

        return View(paginatedAuctions);
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

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isWatched = false;
        if (currentUserId != null)
        {
            isWatched = await _context.Watchlist.AnyAsync(w => w.AuctionId == id && w.UserId == currentUserId);
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
            BuyItNowPrice = auction.BuyItNowPrice,
            EndTime = auction.EndTime,
            Category = auction.Category.Name,
            Seller = auction.Seller.DisplayName,
            SellerId = auction.SellerId,
            IsActive = auction.IsActive && auction.EndTime > DateTime.UtcNow,
            IsSuspended = auction.IsSuspended,
            IsWatched = isWatched,
            Bids = auction.Bids
                .OrderByDescending(b => b.BidTime)
                .Select(b => new BidViewModel
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder.DisplayName
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

        var result = await _auctionService.PlaceBidAsync(auctionId, currentUserId, amount);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    public async Task<IActionResult> BuyItNow(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.BuyItNowAsync(auctionId, currentUserId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpGet]
    public async Task<IActionResult> MyAuctions(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => a.SellerId == currentUserId);

        // Status Filtering
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "active") query = query.Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);
            else if (status == "closed") query = query.Where(a => !a.IsActive || a.EndTime <= DateTime.UtcNow);
        }

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

        if (minPrice.HasValue) query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        // Sorting
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            "oldest" => query.OrderBy(a => a.CreatedOn),
            _ => query.OrderByDescending(a => a.CreatedOn)
        };

        var projectedQuery = query.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive
        });

        int pageSize = 6;
        var paginated = await PaginatedList<AuctionListViewModel>.CreateAsync(projectedQuery, pageNumber ?? 1, pageSize);
        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginated);
    }

    [HttpGet]
    public async Task<IActionResult> MyBids(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        var myBids = _context.Bids.Where(b => b.BidderId == currentUserId);
        
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => myBids.Any(b => b.AuctionId == a.Id) && !adminIds.Contains(a.SellerId));

        // Filtering
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "active") query = query.Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);
            else if (status == "closed") query = query.Where(a => !a.IsActive || a.EndTime <= DateTime.UtcNow);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var normalizedSearch = searchTerm.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(normalizedSearch) || 
                             a.Description.ToLower().Contains(normalizedSearch));
        }

        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);
        if (minPrice.HasValue) query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        // Sorting
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.EndTime)
        };

        var myMaxBids = await myBids
            .GroupBy(b => b.AuctionId)
            .Select(g => new { AuctionId = g.Key, MaxAmount = g.Max(b => b.Amount) })
            .ToDictionaryAsync(x => x.AuctionId, x => x.MaxAmount);

        var projectedQuery = query.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsWinning = myMaxBids.ContainsKey(a.Id) && myMaxBids[a.Id] >= a.CurrentPrice
        });

        int pageSize = 6;
        var paginated = await PaginatedList<AuctionListViewModel>.CreateAsync(projectedQuery, pageNumber ?? 1, pageSize);
        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginated);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> UserAuctions(string username, string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status = "active")
    {
        if (string.IsNullOrEmpty(username)) return NotFound();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (user == null) return NotFound();

        // Check if target user is an Admin
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        bool targetIsAdmin = adminRole != null && await _context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id);
        
        // If target is admin and current viewer is not admin, hide content
        if (targetIsAdmin && !User.IsInRole("Administrator"))
        {
            return NotFound(); // Or redirect to Home with a message
        }

        ViewData["TargetUser"] = user.DisplayName;
        ViewData["TargetUserImage"] = user.ProfilePictureUrl;
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => a.SellerId == user.Id);

        // Status Filtering (Default: active)
        if (status == "active")
        {
            query = query.Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);
        }
        else if (status == "closed")
        {
            query = query.Where(a => !a.IsActive || a.EndTime <= DateTime.UtcNow);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var normalizedSearch = searchTerm.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(normalizedSearch) || 
                             a.Description.ToLower().Contains(normalizedSearch));
        }

        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);
        if (minPrice.HasValue) query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        // Sorting
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.CreatedOn)
        };

        var projectedQuery = query.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive
        });

        int pageSize = 6;
        var paginated = await PaginatedList<AuctionListViewModel>.CreateAsync(projectedQuery, pageNumber ?? 1, pageSize);
        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginated);
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
            BuyItNowPrice = auction.BuyItNowPrice,
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

        ValidateImage(model.ImageFile);

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        string? imagePath = model.ImageUrl;
        if (model.ImageFile != null)
        {
            // Delete old
            DeleteImage(auction.ImageUrl);
            // Save new
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
        auction.BuyItNowPrice = model.BuyItNowPrice;
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

        DeleteImage(auction.ImageUrl);

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
            MinIncrease = 0.10m
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AuctionFormModel model)
    {
        ValidateImage(model.ImageFile);

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
            // If upload happened but state is invalid, should we delete the uploaded file? 
            // In a real app yes, here we skip for simplicity or do it.
            DeleteImage(imagePath);
            
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
            BuyItNowPrice = model.BuyItNowPrice,
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

    [HttpPost]
    public async Task<IActionResult> ToggleWatchlist(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var existingItem = await _context.Watchlist
            .FirstOrDefaultAsync(w => w.AuctionId == id && w.UserId == currentUserId);

        if (existingItem != null)
        {
            _context.Watchlist.Remove(existingItem);
            TempData["Success"] = "Removed from watchlist.";
        }
        else
        {
            var watchItem = new AuctionWatchlist
            {
                AuctionId = id,
                UserId = currentUserId,
                AddedOn = DateTime.UtcNow
            };
            _context.Watchlist.Add(watchItem);
            TempData["Success"] = "Added to watchlist.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> MyWatchlist(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;
        
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var query = _context.Watchlist
            .Where(w => w.UserId == currentUserId)
            .Include(w => w.Auction)
            .ThenInclude(a => a.Category)
            .Select(w => w.Auction)
            .Where(a => !adminIds.Contains(a.SellerId))
            .AsQueryable();

        // Filtering Logic (Reused)
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "active") query = query.Where(a => a.IsActive && a.EndTime > DateTime.UtcNow);
            else if (status == "closed") query = query.Where(a => !a.IsActive || a.EndTime <= DateTime.UtcNow);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var normalizedSearch = searchTerm.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(normalizedSearch) || 
                             a.Description.ToLower().Contains(normalizedSearch));
        }

        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);
        if (minPrice.HasValue) query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        // Sorting
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.EndTime)
        };

        var projectedQuery = query.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive
        });

        int pageSize = 6;
        var paginated = await PaginatedList<AuctionListViewModel>.CreateAsync(projectedQuery, pageNumber ?? 1, pageSize);
        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginated);
    }

    private void ValidateImage(IFormFile? file)
    {
        if (file == null) return;

        if (file.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("ImageFile", "File size must be less than 5MB.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError("ImageFile", "Invalid file type. Allowed: jpg, jpeg, png, gif, webp.");
        }
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

    private void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        
        // Check if it's a local file (starts with our path)
        if (imageUrl.StartsWith("/images/auctions/"))
        {
            // Convert web path to file path
            // Remove leading slash, replace / with system separator
            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath);
            
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch
                {
                    // Log error or ignore
                }
            }
        }
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