using System.Security.Claims;
using AuctionHub.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using AuctionHub.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize]
public class AuctionsController : Controller
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IAuctionService _auctionService;
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuctionsController(
        IWebHostEnvironment webHostEnvironment, 
        IAuctionService auctionService,
        IUserService userService,
        UserManager<ApplicationUser> userManager)
    {
        _webHostEnvironment = webHostEnvironment;
        _auctionService = auctionService;
        _userService = userService;
        _userManager = userManager;
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
        
        int pageSize = 9;
        var paginatedDto = await _auctionService.GetAuctionsAsync(
            searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended,
            IsWinning = a.IsWinning
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync(); 

        return View(paginatedViewModel);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var auction = await _auctionService.GetAuctionDetailsAsync(id, currentUserId);

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
            BuyItNowPrice = auction.BuyItNowPrice,
            EndTime = auction.EndTime,
            Category = auction.Category,
            Seller = auction.Seller,
            SellerId = auction.SellerId,
            IsActive = auction.IsActive,
            IsSuspended = auction.IsSuspended,
            IsWatched = auction.IsWatched,
            Bids = auction.Bids
                .Select(b => new BidViewModel
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder
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
        if (currentUserId == null) return Challenge();

        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        int pageSize = 6;
        var paginatedDto = await _auctionService.GetMyAuctionsAsync(
            currentUserId, searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginatedViewModel);
    }

    [HttpGet]
    public async Task<IActionResult> MyBids(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();
        
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        int pageSize = 6;
        var paginatedDto = await _auctionService.GetMyBidsAsync(
            currentUserId, searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended,
            IsWinning = a.IsWinning
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginatedViewModel);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> UserAuctions(string username, string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status = "active")
    {
        if (string.IsNullOrEmpty(username)) return NotFound();

        var user = await _userService.GetByUsernameAsync(username);
        if (user == null) return NotFound();

        // Check if target user is an Admin
        bool targetIsAdmin = await _userManager.IsInRoleAsync(new ApplicationUser { Id = user.Id }, "Administrator");
        
        // If target is admin and current viewer is not admin, hide content
        if (targetIsAdmin && !User.IsInRole("Administrator"))
        {
            return NotFound(); 
        }

        ViewData["TargetUser"] = user.DisplayName;
        ViewData["TargetUserImage"] = user.ProfilePictureUrl;
        ViewData["CurrentUsername"] = username;
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        int pageSize = 6;
        var paginatedDto = await _auctionService.GetUserAuctionsAsync(
            username, searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginatedViewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var auction = await _auctionService.GetAuctionDetailsAsync(id);

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
            // Strip seconds/milliseconds for the view
            EndTime = new DateTime(auction.EndTime.Year, auction.EndTime.Month, auction.EndTime.Day, 
                                 auction.EndTime.Hour, auction.EndTime.Minute, 0, 0, auction.EndTime.Kind),
            // Category needs to be ID for the dropdown
            CategoryId = auction.CategoryId,
        };

        model.Categories = await GetCategoriesAsync();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AuctionFormModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        ValidateImage(model.ImageFile);

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        string? imagePath = model.ImageUrl;
        if (model.ImageFile != null)
        {
            // Note: In a true Clean Architecture, Image Service would be injected
            imagePath = await SaveImageAsync(model.ImageFile);
        }

        var dto = new AuctionFormDto
        {
            Title = model.Title,
            Description = model.Description,
            ImageUrl = imagePath,
            StartPrice = model.StartPrice,
            MinIncrease = model.MinIncrease,
            BuyItNowPrice = model.BuyItNowPrice,
            EndTime = model.EndTime,
            CategoryId = model.CategoryId
        };

        var result = await _auctionService.UpdateAuctionAsync(id, dto, currentUserId);

        if (result.Success)
        {
            if (!string.IsNullOrEmpty(result.OldImageUrl))
            {
                DeleteImage(result.OldImageUrl);
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }
        else
        {
            if (result.Message == "Forbidden.") return Forbid();
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.DeleteAuctionAsync(id, currentUserId);

        if (result.Success)
        {
            if (!string.IsNullOrEmpty(result.ImageUrl))
            {
                DeleteImage(result.ImageUrl);
            }
            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
        else
        {
            if (result.Message == "Forbidden.") return Forbid();
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var now = DateTime.UtcNow;
        var model = new AuctionFormModel
        {
            Categories = await GetCategoriesAsync(),
            // Strip seconds/milliseconds for initial value
            EndTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, 0, now.Kind).AddDays(7),
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
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var dto = new AuctionFormDto
        {
            Title = model.Title,
            Description = model.Description,
            ImageUrl = imagePath,
            StartPrice = model.StartPrice,
            MinIncrease = model.MinIncrease,
            BuyItNowPrice = model.BuyItNowPrice,
            EndTime = model.EndTime,
            CategoryId = model.CategoryId
        };

        var id = await _auctionService.CreateAuctionAsync(dto, currentUserId);

        if (id == -1)
        {
            // Idempotent behavior: The auction was likely already created by a previous rapid request.
            // Instead of showing an error, we redirect to Index to provide a smooth experience.
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleWatchlist(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.ToggleWatchlistAsync(id, currentUserId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> MyWatchlist(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;
        
        int pageSize = 6;
        var paginatedDto = await _auctionService.GetMyWatchlistAsync(
            currentUserId, searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginatedViewModel);
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
        var categories = await _auctionService.GetCategoriesAsync();
        return categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        }).ToList();
    }
}