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
using Microsoft.AspNetCore.RateLimiting;

namespace AuctionHub.Controllers;

[Authorize]
[EnableRateLimiting("fixed")]
public class AuctionsController : Controller
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IAuctionService _auctionService;
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IChatService _chatService;
    private readonly IReviewService _reviewService;
    private readonly ILogger<AuctionsController> _logger;

    public AuctionsController(
        IWebHostEnvironment webHostEnvironment, 
        IAuctionService auctionService,
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        IChatService chatService,
        IReviewService reviewService,
        ILogger<AuctionsController> logger)
    {
        _webHostEnvironment = webHostEnvironment;
        _auctionService = auctionService;
        _userService = userService;
        _userManager = userManager;
        _chatService = chatService;
        _reviewService = reviewService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status, double? latitude, double? longitude, double? maxDistance)
    {
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;
        ViewData["Latitude"] = latitude;
        ViewData["Longitude"] = longitude;
        ViewData["MaxDistance"] = maxDistance;
        
        int pageSize = 9;
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var paginatedDto = await _auctionService.GetAuctionsAsync(
            searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status, currentUserId, latitude, longitude, maxDistance);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            PublicId = a.PublicId,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            City = a.City,
            Country = a.Country,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            IsWinning = a.IsWinning,
            WinnerId = a.WinnerId,
            SellerName = a.SellerName,
            SellerId = a.SellerId,
            SellerPublicId = a.SellerPublicId,
            IsTopSeller = a.IsTopSeller
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync(); 

        return View(paginatedViewModel);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var auction = await _auctionService.GetAuctionDetailsByPublicIdAsync(id, currentUserId);

        if (auction == null)
        {
            return NotFound();
        }

        ViewBag.IsFollowing = currentUserId != null && await _userService.IsFollowingAsync(currentUserId, auction.SellerId);

        var model = new AuctionDetailsViewModel
        {
            Id = auction.Id,
            PublicId = auction.PublicId,
            Title = auction.Title,
            Description = auction.Description,
            ImageUrl = auction.ImageUrl,
            CurrentPrice = auction.CurrentPrice,
            StartPrice = auction.StartPrice,
            MinIncrease = auction.MinIncrease,
            BuyItNowPrice = auction.BuyItNowPrice,
            ReservePrice = auction.ReservePrice,
            ReservePriceMet = auction.ReservePriceMet,
            IsDutchAuction = auction.IsDutchAuction,
            DutchDecrementAmount = auction.DutchDecrementAmount,
            DutchDecrementIntervalMinutes = auction.DutchDecrementIntervalMinutes,
            NextDutchDecrement = auction.NextDutchDecrement,
            EndTime = auction.EndTime,
            Category = auction.Category,
            Images = auction.Images.Select(i => i.Url).ToList(),
            Seller = auction.Seller,
            SellerId = auction.SellerId,
            SellerPublicId = auction.SellerPublicId,
            SellerRating = auction.SellerRating,
            SellerReviewCount = auction.SellerReviewCount,
            IsActive = auction.IsActive,
            IsDelivered = auction.IsDelivered,
            IsSettled = auction.IsSettled,
            IsDisputed = auction.IsDisputed,
            IsSuspended = auction.IsSuspended,
            IsWatched = auction.IsWatched,
            IsWinning = auction.IsWinning,
            WinnerId = auction.WinnerId,
            CurrentAutoBidLimit = auction.CurrentAutoBidLimit,
            Bids = auction.Bids
                .Select(b => new BidViewModel
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder
                })
                .ToList(),
            PrivateOffers = auction.PrivateOffers,
            Comments = auction.Comments,
            NewBidAmount = auction.CurrentPrice + auction.MinIncrease
        };

        // Check if user can leave a review
        if (currentUserId != null)
        {
            ViewBag.CanLeaveReview = await _reviewService.CanReviewAsync(auction.Id, currentUserId);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeaveReview(int auctionId, int rating, string comment)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var reviewDto = new ReviewDto
        {
            AuctionId = auctionId,
            ReviewerId = currentUserId,
            Rating = rating,
            Comment = comment
        };

        var success = await _reviewService.AddReviewAsync(reviewDto);

        if (success)
        {
            TempData["Success"] = "Thank you for your feedback!";
        }
        else
        {
            TempData["Error"] = "Could not submit review. Please ensure you are the winner and the auction is closed.";
        }

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDelivery(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.ConfirmDeliveryAsync(auctionId, currentUserId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId);
        return RedirectToAction(nameof(Details), new { id = auction?.PublicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promote(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.PromoteAuctionAsync(auctionId, currentUserId);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(MyAuctions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("bidding")]
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

        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId);
        return RedirectToAction(nameof(Details), new { id = auction?.PublicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("bidding")]
    public async Task<IActionResult> SetAutoBid(int auctionId, decimal maxAmount)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.SetAutoBidAsync(auctionId, currentUserId, maxAmount);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId);
        return RedirectToAction(nameof(Details), new { id = auction?.PublicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("bidding")]
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

        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId);
        return RedirectToAction(nameof(Details), new { id = auction?.PublicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int auctionId, string reason, string details)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.ReportAuctionAsync(auctionId, currentUserId, reason, details);

        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId);
        return RedirectToAction(nameof(Details), new { id = auction?.PublicId });
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
            PublicId = a.PublicId,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            City = a.City,
            Country = a.Country,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerName = a.SellerName,
            SellerId = a.SellerId,
            SellerPublicId = a.SellerPublicId,
            IsTopSeller = a.IsTopSeller,
            IsWinning = a.IsWinning,
            WinnerId = a.WinnerId
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
            PublicId = a.PublicId,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            City = a.City,
            Country = a.Country,
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
    public async Task<IActionResult> UserAuctions(Guid id, string? searchTerm, int? categoryId, string? sortOrder, int? pageNumber, decimal? minPrice, decimal? maxPrice, string? status = "active")
    {
        var user = await _userService.GetByPublicIdAsync(id);
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
        ViewData["TargetUserAboutMe"] = user.AboutMe;
        
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.IsFollowing = currentUserId != null && await _userService.IsFollowingAsync(currentUserId, user.Id);
        ViewBag.TargetUserId = user.Id;
        ViewBag.TargetUserPublicId = user.PublicId;
        ViewData["TargetUserRating"] = user.AverageRating;
        ViewData["TargetUserIsTopSeller"] = user.IsTopSeller;
        ViewData["TargetUserFollowersCount"] = user.FollowersCount;
        ViewData["TargetUserFollowingCount"] = user.FollowingCount;
        ViewData["TargetUserReviews"] = user.Reviews;
        ViewData["CurrentUsername"] = user.UserName;
        ViewData["CurrentSort"] = sortOrder;
        ViewData["CurrentSearch"] = searchTerm;
        ViewData["CurrentCategory"] = categoryId;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["Status"] = status;

        int pageSize = 6;
        var paginatedDto = await _auctionService.GetUserAuctionsAsync(
            user.UserName, searchTerm, categoryId, sortOrder, pageNumber ?? 1, pageSize, minPrice, maxPrice, status);

        var viewModelItems = paginatedDto.Select(a => new AuctionListViewModel
        {
            Id = a.Id,
            PublicId = a.PublicId,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            City = a.City,
            Country = a.Country,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerName = a.SellerName,
            SellerId = a.SellerId,
            SellerPublicId = a.SellerPublicId,
            IsTopSeller = a.IsTopSeller,
            IsWinning = a.IsWinning,
            WinnerId = a.WinnerId
        }).ToList();

        var paginatedViewModel = new PaginatedList<AuctionListViewModel>(
            viewModelItems, paginatedDto.TotalCount, paginatedDto.PageIndex, pageSize);

        ViewBag.Categories = await GetCategoriesAsync();

        return View(paginatedViewModel);
    }

    [HttpGet]
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
            ReservePrice = auction.ReservePrice,
            IsDutchAuction = auction.IsDutchAuction,
            DutchDecrementAmount = auction.DutchDecrementAmount,
            DutchDecrementIntervalMinutes = auction.DutchDecrementIntervalMinutes,
            EndTime = new DateTime(auction.EndTime.Year, auction.EndTime.Month, auction.EndTime.Day, 
                                 auction.EndTime.Hour, auction.EndTime.Minute, 0, 0, auction.EndTime.Kind),
            CategoryId = auction.CategoryId,
            ExistingImages = auction.Images
        };

        model.Categories = await GetCategoriesAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AuctionFormModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        ValidateImage(model.ImageFile);
        if (model.AdditionalImageFiles != null)
        {
            foreach (var file in model.AdditionalImageFiles)
            {
                ValidateImage(file);
            }
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var dto = new AuctionFormDto
        {
            Title = model.Title,
            Description = model.Description,
            ImageUrl = model.ImageUrl,
            StartPrice = model.StartPrice,
            MinIncrease = model.MinIncrease,
            BuyItNowPrice = model.BuyItNowPrice,
            ReservePrice = model.ReservePrice,
            IsDutchAuction = model.IsDutchAuction,
            DutchDecrementAmount = model.DutchDecrementAmount,
            DutchDecrementIntervalMinutes = model.DutchDecrementIntervalMinutes,
            EndTime = model.EndTime,
            CategoryId = model.CategoryId
        };

        // Handle new files
        if (model.ImageFile != null)
        {
            dto.ImageStreams.Add(model.ImageFile.OpenReadStream());
            dto.ImageFileNames.Add(model.ImageFile.FileName);
        }

        if (model.AdditionalImageFiles != null)
        {
            foreach (var file in model.AdditionalImageFiles)
            {
                if (file.Length > 0)
                {
                    dto.ImageStreams.Add(file.OpenReadStream());
                    dto.ImageFileNames.Add(file.FileName);
                }
            }
        }

        // Handle JSON removals
        if (!string.IsNullOrEmpty(model.ImagesToRemoveIdsJson))
        {
            try
            {
                dto.ImagesToRemoveIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(model.ImagesToRemoveIdsJson) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize ImagesToRemoveIdsJson.");
            }
        }

        // Handle JSON additional URLs
        if (!string.IsNullOrEmpty(model.AdditionalImageUrlsJson))
        {
            try
            {
                var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(model.AdditionalImageUrlsJson);
                if (urls != null) dto.AdditionalImageUrls.AddRange(urls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize AdditionalImageUrlsJson during edit.");
            }
        }

        var result = await _auctionService.UpdateAuctionAsync(id, dto, currentUserId);

        if (result.Success)
        {
            TempData["Success"] = "Listing updated successfully.";
            return RedirectToAction(nameof(Details), new { id = id });
        }
        
        if (result.Message == "Forbidden.") return Forbid();
        TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.DeleteAuctionAsync(id, currentUserId);

        if (result.Success)
        {
            if (result.ImageUrls != null)
            {
                foreach (var url in result.ImageUrls)
                {
                    DeleteImage(url);
                }
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AuctionFormModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        // 1. Validation
        ValidateImage(model.ImageFile);
        if (model.AdditionalImageFiles != null)
        {
            foreach (var file in model.AdditionalImageFiles)
            {
                ValidateImage(file);
            }
        }

        if (!ModelState.IsValid)
        {
            var errors = string.Join(" | ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            TempData["Error"] = "Validation failed: " + errors;
            
            model.Categories = await GetCategoriesAsync();
            return View(model);
        }

        // 2. Prepare DTO
        var dto = new AuctionFormDto
        {
            Title = model.Title,
            Description = model.Description,
            ImageUrl = model.ImageUrl, // Main URL if provided
            StartPrice = model.StartPrice,
            MinIncrease = model.MinIncrease,
            BuyItNowPrice = model.BuyItNowPrice,
            ReservePrice = model.ReservePrice,
            IsDutchAuction = model.IsDutchAuction,
            DutchDecrementAmount = model.DutchDecrementAmount,
            DutchDecrementIntervalMinutes = model.DutchDecrementIntervalMinutes,
            EndTime = model.EndTime,
            CategoryId = model.CategoryId,
            ShouldPromote = model.ShouldPromote
        };

        // Process File Streams
        if (model.ImageFile != null)
        {
            dto.ImageStreams.Add(model.ImageFile.OpenReadStream());
            dto.ImageFileNames.Add(model.ImageFile.FileName);
        }

        if (model.AdditionalImageFiles != null)
        {
            foreach (var file in model.AdditionalImageFiles)
            {
                if (file.Length > 0)
                {
                    dto.ImageStreams.Add(file.OpenReadStream());
                    dto.ImageFileNames.Add(file.FileName);
                }
            }
        }

        // Process JSON URLs
        if (!string.IsNullOrEmpty(model.AdditionalImageUrlsJson))
        {
            try
            {
                var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(model.AdditionalImageUrlsJson);
                if (urls != null) dto.AdditionalImageUrls.AddRange(urls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize AdditionalImageUrlsJson during creation.");
            }
        }

        // 3. Call Service
        int auctionId;
        string message;
        (auctionId, message) = await _auctionService.CreateAuctionAsync(dto, currentUserId);

        if (auctionId > 0)
        {
            TempData["Success"] = "Your auction is live!";
            return RedirectToAction(nameof(Details), new { id = auctionId });
        }
        
        if (auctionId == -1)
        {
            TempData["Error"] = "A similar auction was recently created. Please wait a few seconds.";
        }
        else if (auctionId == -2)
        {
            TempData["Error"] = message; // AI Moderation Failed
        }
        else
        {
            TempData["Error"] = "Failed to list item. Please try again.";
        }

        return RedirectToAction(nameof(MyAuctions));
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
            PublicId = a.PublicId,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category,
            City = a.City,
            Country = a.Country,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerName = a.SellerName,
            SellerId = a.SellerId,
            SellerPublicId = a.SellerPublicId,
            IsTopSeller = a.IsTopSeller,
            IsWinning = a.IsWinning,
            WinnerId = a.WinnerId
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

        // Validate MIME type (Content-Type)
        var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!string.IsNullOrEmpty(file.ContentType) && !allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            ModelState.AddModelError("ImageFile", "Invalid file type. Must be an image (JPEG, PNG, GIF, or WebP).");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError("ImageFile", "Invalid file extension. Allowed: jpg, jpeg, png, gif, webp.");
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

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.CancelAuctionAsync(id, currentUserId);
        if (result.Success)
        {
            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAutoBid(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.DeactivateAutoBidAsync(id, currentUserId);
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispute(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.DisputeAuctionAsync(id, currentUserId);
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("bidding")]
    public async Task<IActionResult> MakePrivateOffer(int auctionId, decimal offerAmount)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.MakePrivateOfferAsync(auctionId, currentUserId, offerAmount);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptPrivateOffer(int offerId, int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.AcceptPrivateOfferAsync(offerId, currentUserId);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPrivateOffer(int offerId, int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.RejectPrivateOfferAsync(offerId, currentUserId);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayParticipationFee(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.PayParticipationFeeAsync(auctionId, currentUserId);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(string sellerId, Guid publicId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _userService.FollowUserAsync(currentUserId, sellerId);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(UserAuctions), new { id = publicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(string sellerId, Guid publicId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _userService.UnfollowUserAsync(currentUserId, sellerId);
        
        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;

        return RedirectToAction(nameof(UserAuctions), new { id = publicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int auctionId, string content)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var result = await _auctionService.AddCommentAsync(auctionId, currentUserId, content);
        
        if (result.Success)
        {
            TempData["Success"] = "Comment added successfully.";
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = auctionId });
    }
}