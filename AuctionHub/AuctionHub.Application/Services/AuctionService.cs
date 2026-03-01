using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AuctionHub.Application.Services;

public class AuctionService : IAuctionService
{
    private readonly IAuctionHubDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IBiddingNotificationService _biddingNotificationService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuctionService> _logger;
    private readonly IPhotoService _photoService;

    public AuctionService(
        IAuctionHubDbContext context, 
        INotificationService notificationService, 
        IBiddingNotificationService biddingNotificationService,
        IDistributedCache cache,
        ILogger<AuctionService> logger,
        IPhotoService photoService)
    {
        _context = context;
        _notificationService = notificationService;
        _biddingNotificationService = biddingNotificationService;
        _cache = cache;
        _logger = logger;
        _photoService = photoService;
    }

    public async Task<PaginatedList<AuctionDto>> GetAuctionsAsync(
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status,
        string? currentUserId = null)
    {
        // Get Admin IDs to exclude their test auctions from public view
        var adminIds = await GetAdminIdsAsync();

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
            "price_desc" => query.OrderByDescending(a => a.IsPromoted).ThenByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderByDescending(a => a.IsPromoted).ThenBy(a => a.CurrentPrice),
            "newest" => query.OrderByDescending(a => a.IsPromoted).ThenByDescending(a => a.CreatedOn),
            _ => query.OrderByDescending(a => a.IsPromoted).ThenBy(a => a.EndTime) // Default: Ending soonest
        };

        var projectedQuery = query
            .Include(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(a => a.Bids)
            .Select(a => new AuctionDto
            {
                Id = a.Id,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category.Name,
                CategoryId = a.CategoryId,
                IsActive = a.IsActive,
                IsPromoted = a.IsPromoted,
                IsSuspended = a.IsSuspended,
                SellerId = a.SellerId,
                SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
                IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
                IsWinning = currentUserId != null && a.Bids.Any(b => b.BidderId == currentUserId) 
                    ? a.Bids.OrderByDescending(b => b.Amount).First().BidderId == currentUserId 
                    : (bool?)null
            });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<PaginatedList<AuctionDto>> GetMyAuctionsAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status)
    {
        var query = _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(a => a.Bids)
            .Where(a => a.SellerId == userId);

        // Filtering & Sorting (Same logic)
        query = ApplyFilters(query, searchTerm, categoryId, minPrice, maxPrice, status);
        
        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            "oldest" => query.OrderBy(a => a.CreatedOn),
            _ => query.OrderByDescending(a => a.CreatedOn)
        };

        var projectedQuery = query.Select(a => new AuctionDto
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerId = a.SellerId,
            SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
            IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
            IsWinning = (bool?)null
        });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<PaginatedList<AuctionDto>> GetMyBidsAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status)
    {
        var myBids = _context.Bids.Where(b => b.BidderId == userId);
        
        var adminIds = await GetAdminIdsAsync();

        var query = _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(a => a.Bids)
            .Where(a => myBids.Any(b => b.AuctionId == a.Id) && !adminIds.Contains(a.SellerId));

        query = ApplyFilters(query, searchTerm, categoryId, minPrice, maxPrice, status);

        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.EndTime)
        };

        var projectedQuery = query.Select(a => new AuctionDto
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerId = a.SellerId,
            SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
            IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
            IsWinning = a.Bids.Any(b => b.BidderId == userId) 
                ? a.Bids.OrderByDescending(b => b.Amount).First().BidderId == userId 
                : (bool?)null
        });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<PaginatedList<AuctionDto>> GetUserAuctionsAsync(
        string username,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (user == null) return new PaginatedList<AuctionDto>(new List<AuctionDto>(), 0, pageNumber, pageSize);

        var query = _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(a => a.Bids)
            .Where(a => a.SellerId == user.Id);

        query = ApplyFilters(query, searchTerm, categoryId, minPrice, maxPrice, status);

        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.CreatedOn)
        };

        var projectedQuery = query.Select(a => new AuctionDto
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerId = a.SellerId,
            SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
            IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
            IsWinning = (bool?)null
        });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<PaginatedList<AuctionDto>> GetMyWatchlistAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status)
    {
        var adminIds = await GetAdminIdsAsync();

        var query = _context.Watchlist
            .Where(w => w.UserId == userId)
            .Include(w => w.Auction)
                .ThenInclude(a => a.Category)
            .Include(w => w.Auction)
                .ThenInclude(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(w => w.Auction)
                .ThenInclude(a => a.Bids)
            .Select(w => w.Auction)
            .Where(a => !adminIds.Contains(a.SellerId))
            .AsQueryable();

        query = ApplyFilters(query, searchTerm, categoryId, minPrice, maxPrice, status);

        query = sortOrder switch
        {
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            _ => query.OrderByDescending(a => a.EndTime)
        };

        var projectedQuery = query.Select(a => new AuctionDto
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsPromoted = a.IsPromoted,
            IsSuspended = a.IsSuspended,
            SellerId = a.SellerId,
            SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
            IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
            IsWinning = a.Bids.Any(b => b.BidderId == userId) 
                ? a.Bids.OrderByDescending(b => b.Amount).First().BidderId == userId 
                : (bool?)null
        });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<IEnumerable<AuctionDto>> GetEndingSoonAuctionsAsync(int count, string? currentUserId = null)
    {
        string cacheKey = $"ending_soon_{count}_{currentUserId ?? "anonymous"}";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            _logger.LogInformation("Returning cached ending soon auctions.");
            return JsonSerializer.Deserialize<IEnumerable<AuctionDto>>(cachedData) ?? new List<AuctionDto>();
        }

        var adminIds = await GetAdminIdsAsync();
        var now = DateTime.UtcNow;

        var auctionsQuery = _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
                .ThenInclude(u => u.ReceivedReviews)
            .Include(a => a.Bids)
            .Where(a => a.IsActive && a.EndTime > now && !adminIds.Contains(a.SellerId));

        if (currentUserId != null)
        {
            auctionsQuery = auctionsQuery.Where(a => a.SellerId != currentUserId);
        }

        var auctions = await auctionsQuery
            .OrderByDescending(a => a.IsPromoted)
            .ThenBy(a => a.EndTime)
            .Take(count)
            .Select(a => new AuctionDto
            {
                Id = a.Id,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category.Name,
                CategoryId = a.CategoryId,
                IsActive = a.IsActive,
                IsPromoted = a.IsPromoted,
                IsSuspended = a.IsSuspended,
                SellerId = a.SellerId,
                SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
                IsTopSeller = a.Seller.ReceivedReviews.Count >= 5 && (a.Seller.ReceivedReviews.Any() ? a.Seller.ReceivedReviews.Average(r => r.Rating) : 0) >= 4.8,
                IsWinning = currentUserId != null && a.Bids.Any(b => b.BidderId == currentUserId) 
                    ? a.Bids.OrderByDescending(b => b.Amount).First().BidderId == currentUserId 
                    : (bool?)null
            })
            .ToListAsync();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(auctions), options);

        return auctions;
    }

    public async Task<(bool Success, string Message)> ConfirmDeliveryAsync(int auctionId, string userId)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var auction = await _context.Auctions
                .Include(a => a.Seller)
                .Include(a => a.Bids)
                .FirstOrDefaultAsync(a => a.Id == auctionId);

            if (auction == null) return (false, "Auction not found.");
            if (auction.IsActive && auction.EndTime > DateTime.UtcNow) return (false, "Auction is still active.");
            if (auction.IsSettled) return (false, "Funds have already been released.");
            if (auction.IsDisputed) return (false, "This auction is currently under dispute.");

            var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (winningBid == null || winningBid.BidderId != userId)
            {
                return (false, "Only the auction winner can confirm delivery.");
            }

            // Release Funds to Seller
            decimal price = winningBid.Amount;
            auction.Seller.WalletBalance += price;
            auction.IsSettled = true;

            _context.Transactions.Add(new Transaction
            {
                UserId = auction.SellerId,
                Amount = price,
                Description = $"Sale of item '{auction.Title}' - Escrow released (Auction ID: {auctionId})",
                TransactionType = "Sale",
                TransactionDate = DateTime.UtcNow,
                AuctionId = auctionId
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            await _notificationService.NotifyUserAsync(auction.SellerId, 
                $"💰 Payment for '{auction.Title}' has been released to your wallet! The buyer confirmed delivery.", 
                $"/Auctions/Details/{auctionId}");

            return (true, "Delivery confirmed! Funds have been released to the seller.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred during delivery confirmation.");
        }
    }

    public async Task<(bool Success, string Message)> CancelAuctionAsync(int auctionId, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return (false, "Auction not found.");
        if (auction.SellerId != userId) return (false, "You can only cancel your own auctions.");
        if (auction.Bids.Any()) return (false, "You cannot cancel an auction that has bids.");
        if (!auction.IsActive) return (false, "Auction is already closed.");

        auction.IsActive = false;
        auction.IsDeleted = true;

        await _context.SaveChangesAsync();
        return (true, "Auction cancelled successfully.");
    }

    public async Task<(bool Success, string Message)> DeactivateAutoBidAsync(int auctionId, string userId)
    {
        var autoBid = await _context.AutoBids
            .FirstOrDefaultAsync(ab => ab.AuctionId == auctionId && ab.UserId == userId && ab.IsActive);

        if (autoBid == null) return (false, "No active auto-bid found.");

        autoBid.IsActive = false;
        await _context.SaveChangesAsync();
        return (true, "Auto-bid deactivated.");
    }

    public async Task<(bool Success, string Message)> DisputeAuctionAsync(int auctionId, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return (false, "Auction not found.");
        if (auction.IsSettled) return (false, "Cannot dispute an auction after funds have been released.");
        
        var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (winningBid == null || winningBid.BidderId != userId) 
            return (false, "Only the winner can open a dispute.");

        auction.IsDisputed = true;
        
        await _notificationService.NotifyUserAsync(auction.SellerId, 
            $"⚠️ A dispute has been opened for '{auction.Title}'. Escrow funds are frozen until resolved by an administrator.", 
            $"/Auctions/Details/{auctionId}");

        await _context.SaveChangesAsync();
        return (true, "Dispute opened. Our team will review the transaction.");
    }

    public async Task<(bool Success, string Message)> PromoteAuctionAsync(int auctionId, string userId)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var auction = await _context.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId);
            if (auction == null) return (false, "Auction not found.");
            if (auction.SellerId != userId) return (false, "Forbidden.");
            if (auction.IsPromoted) return (false, "This auction is already promoted.");
            if (!auction.IsActive) return (false, "Cannot promote a closed auction.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            // Use Dynamic System Setting for Promotion Fee
            var feeSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "PromotionFee");
            decimal promotionFee = feeSetting != null ? decimal.Parse(feeSetting.Value) : 5.00m; 
            
            if (user.WalletBalance < promotionFee) return (false, $"Insufficient funds. Promotion costs {promotionFee:C}.");

            // 1. Charge User
            user.WalletBalance -= promotionFee;
            _context.Transactions.Add(new Transaction
            {
                UserId = userId,
                Amount = promotionFee,
                Description = $"Promoted auction: '{auction.Title}'",
                TransactionType = "Promotion",
                TransactionDate = DateTime.UtcNow,
                AuctionId = auctionId
            });

            // 2. Mark Auction as Promoted
            auction.IsPromoted = true;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return (true, "Successfully promoted! Your auction will now appear highlighted at the top of lists.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred while promoting the auction.");
        }
    }

    public async Task<(bool Success, string Message)> ReportAuctionAsync(int auctionId, string userId, string reason, string details)
    {
        try
        {
            var auction = await _context.Auctions.FindAsync(auctionId);
            if (auction == null) return (false, "Auction not found.");

            var report = new UserReport
            {
                ReporterId = userId,
                ReportedAuctionId = auctionId,
                Reason = reason,
                Details = details,
                CreatedOn = DateTime.UtcNow,
                IsResolved = false
            };

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();

            return (true, "Your report has been submitted. Our team will review it shortly.");
        }
        catch (Exception)
        {
            return (false, "An error occurred while submitting the report.");
        }
    }

    private async Task<List<string>> GetAdminIdsAsync()
    {
        string cacheKey = "admin_user_ids";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<string>>(cachedData) ?? new List<string>();
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(adminIds), options);

        return adminIds;
    }

    public async Task<AuctionDetailsDto?> GetAuctionDetailsAsync(int id, string? currentUserId = null)
    {
        var auction = await _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .Include(a => a.Images)
            .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return null;

        // Fetch seller rating statistics
        var reviews = await _context.Reviews.Where(r => r.TargetUserId == auction.SellerId).ToListAsync();
        double sellerRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
        int reviewCount = reviews.Count;

        bool isWatched = false;
        decimal? currentAutoBidLimit = null;

        if (currentUserId != null)
        {
            isWatched = await _context.Watchlist.AnyAsync(w => w.AuctionId == id && w.UserId == currentUserId);
            
            // Fetch active auto-bid limit for the current user
            var autoBid = await _context.AutoBids
                .FirstOrDefaultAsync(ab => ab.AuctionId == id && ab.UserId == currentUserId && ab.IsActive);
            currentAutoBidLimit = autoBid?.MaxAmount;
        }

        return new AuctionDetailsDto
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
            CategoryId = auction.CategoryId,
            Images = auction.Images.Select(i => new AuctionImageDto { Id = i.Id, Url = i.Url }).ToList(),
            Seller = auction.Seller.DisplayName,
            SellerId = auction.SellerId,
            SellerRating = sellerRating,
            SellerReviewCount = reviewCount,
            IsActive = auction.IsActive && auction.EndTime > DateTime.UtcNow,
            IsDelivered = await _context.Transactions.AnyAsync(t => t.UserId == auction.SellerId && t.TransactionType == "Sale" && t.AuctionId == id),
            IsSettled = auction.IsSettled,
            IsDisputed = auction.IsDisputed,
            IsSuspended = auction.IsSuspended,
            IsWatched = isWatched,
            IsWinning = currentUserId != null && auction.Bids.Any(b => b.BidderId == currentUserId) 
                ? auction.Bids.OrderByDescending(b => b.Amount).First().BidderId == currentUserId 
                : (bool?)null,
            CurrentAutoBidLimit = currentAutoBidLimit,
            WinnerId = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault()?.BidderId,
            Bids = auction.Bids
                .OrderByDescending(b => b.BidTime)
                .Select(b => new BidDto
                {
                    Amount = b.Amount,
                    BidTime = b.BidTime,
                    Bidder = b.Bidder.DisplayName
                })
                .ToList()
        };
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        string cacheKey = "all_categories";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<IEnumerable<CategoryDto>>(cachedData) ?? new List<CategoryDto>();
        }

        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(categories), options);

        return categories;
    }

    public async Task<int> CreateAuctionAsync(AuctionFormDto model, string sellerId)
    {
        var now = DateTime.UtcNow;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Database-level check for duplicate within a short timeframe
            // This is safer in multi-instance environments than in-memory dictionaries
            var recentThreshold = now.AddSeconds(-30);
            var isDuplicate = await _context.Auctions.AnyAsync(a => 
                a.SellerId == sellerId && 
                a.Title == model.Title && 
                a.CreatedOn >= recentThreshold);

            if (isDuplicate)
            {
                return -1;
            }

            var auction = new Auction
            {
                Title = model.Title,
                Description = model.Description,
                ImageUrl = model.ImageUrl, // Default or first image
                StartPrice = model.StartPrice,
                CurrentPrice = model.StartPrice,
                MinIncrease = model.MinIncrease,
                BuyItNowPrice = model.BuyItNowPrice,
                EndTime = new DateTime(model.EndTime.Year, model.EndTime.Month, model.EndTime.Day, 
                                     model.EndTime.Hour, model.EndTime.Minute, 0, 0, model.EndTime.Kind),
                CreatedOn = now,
                IsActive = true,
                IsPromoted = model.ShouldPromote,
                CategoryId = model.CategoryId,
                SellerId = sellerId,
                RowVersion = new byte[8]
            };

            // --- Handle Images ---
            var imageList = new List<AuctionImage>();

            // 1. Upload new files to Cloudinary
            for (int i = 0; i < model.ImageStreams.Count; i++)
            {
                var uploadResult = await _photoService.AddPhotoAsync(model.ImageStreams[i], model.ImageFileNames[i]);
                if (uploadResult.Success)
                {
                    imageList.Add(new AuctionImage { Url = uploadResult.Url, PublicId = uploadResult.PublicId });
                }
            }

            // 2. Add external URLs
            foreach (var url in model.AdditionalImageUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    imageList.Add(new AuctionImage { Url = url });
                }
            }

            // 3. Set Cover Image (if not already set or if gallery has items)
            if (imageList.Any())
            {
                auction.Images = imageList;
                if (string.IsNullOrEmpty(auction.ImageUrl))
                {
                    auction.ImageUrl = imageList.First().Url;
                }
            }

            // --- Handle Initial Promotion ---
            if (model.ShouldPromote)
            {
                var user = await _context.Users.FindAsync(sellerId);
                decimal promoFee = 5.00m;
                if (user != null && user.WalletBalance >= promoFee)
                {
                    user.WalletBalance -= promoFee;
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = sellerId,
                        Amount = promoFee,
                        Description = $"Promotion for new auction: '{auction.Title}'",
                        TransactionType = "Promotion",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auction.Id
                    });
                }
                else
                {
                    auction.IsPromoted = false; // Insufficient funds, silently fail promotion but create auction
                }
            }

            _context.Auctions.Add(auction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return auction.Id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<(bool Success, string Message, string? OldImageUrl)> UpdateAuctionAsync(int id, AuctionFormDto model, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return (false, "Auction not found.", null);
        if (auction.SellerId != userId) return (false, "Forbidden.", null);
        if (auction.Bids.Any()) return (false, "You cannot edit an auction that has existing bids.", null);

        string? oldImageUrl = null;

        // 1. Remove selected images
        if (model.ImagesToRemoveIds.Any())
        {
            var imagesToRemove = auction.Images.Where(i => model.ImagesToRemoveIds.Contains(i.Id)).ToList();
            foreach (var img in imagesToRemove)
            {
                if (!string.IsNullOrEmpty(img.PublicId))
                {
                    await _photoService.DeletePhotoAsync(img.PublicId);
                }
                auction.Images.Remove(img);
            }
        }

        // 2. Upload new files to Cloudinary
        for (int i = 0; i < model.ImageStreams.Count; i++)
        {
            var uploadResult = await _photoService.AddPhotoAsync(model.ImageStreams[i], model.ImageFileNames[i]);
            if (uploadResult.Success)
            {
                auction.Images.Add(new AuctionImage { Url = uploadResult.Url, PublicId = uploadResult.PublicId });
            }
        }

        // 3. Add new external URLs
        foreach (var url in model.AdditionalImageUrls)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                auction.Images.Add(new AuctionImage { Url = url });
            }
        }

        // 4. Update Main Image
        if (!string.IsNullOrEmpty(model.ImageUrl))
        {
            auction.ImageUrl = model.ImageUrl;
        }
        else if (auction.Images.Any() && string.IsNullOrEmpty(auction.ImageUrl))
        {
            auction.ImageUrl = auction.Images.First().Url;
        }

        auction.Title = model.Title;
        auction.Description = model.Description;
        auction.StartPrice = model.StartPrice;
        auction.MinIncrease = model.MinIncrease;
        auction.BuyItNowPrice = model.BuyItNowPrice;
        auction.EndTime = new DateTime(model.EndTime.Year, model.EndTime.Month, model.EndTime.Day, 
                                     model.EndTime.Hour, model.EndTime.Minute, 0, 0, model.EndTime.Kind);
        auction.CategoryId = model.CategoryId;

        await _context.SaveChangesAsync();
        return (true, "Auction updated successfully.", oldImageUrl);
    }

    public async Task<(bool Success, string Message, string? ImageUrl)> DeleteAuctionAsync(int id, string userId)
    {
        // Use IgnoreQueryFilters to find the auction even if it was already soft-deleted (just in case)
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return (false, "Auction not found.", null);
        if (auction.SellerId != userId) return (false, "Forbidden.", null);
        
        // Logical check: If it has bids, we shouldn't even archive it if we want to be strict, 
        // but typically archiving is exactly for items with history.
        // For now, let's keep the 'no bids' rule for user deletion to prevent abuse.
        if (auction.Bids.Any()) return (false, "Cannot delete an auction that already has bids.", null);

        string? imageUrl = auction.ImageUrl;

        // Perform Soft Delete (Archive)
        auction.IsDeleted = true;
        auction.IsActive = false;
        
        // We don't delete images from Cloudinary during soft delete 
        // because we might need them for historical audit trails.

        await _context.SaveChangesAsync();

        return (true, "Auction archived successfully.", imageUrl);
    }

    public async Task<(bool Success, string Message)> ToggleWatchlistAsync(int auctionId, string userId)
    {
        var existingItem = await _context.Watchlist
            .FirstOrDefaultAsync(w => w.AuctionId == auctionId && w.UserId == userId);

        if (existingItem != null)
        {
            _context.Watchlist.Remove(existingItem);
            await _context.SaveChangesAsync();
            return (true, "Removed from watchlist.");
        }
        else
        {
            var watchItem = new AuctionWatchlist
            {
                AuctionId = auctionId,
                UserId = userId,
                AddedOn = DateTime.UtcNow
            };
            _context.Watchlist.Add(watchItem);
            await _context.SaveChangesAsync();
            return (true, "Added to watchlist.");
        }
    }

    private IQueryable<Auction> ApplyFilters(IQueryable<Auction> query, string? searchTerm, int? categoryId, decimal? minPrice, decimal? maxPrice, string? status)
    {
        // Status Filtering
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

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (minPrice.HasValue) query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        return query;
    }

    public async Task<(bool Success, string Message)> SetAutoBidAsync(int auctionId, string userId, decimal maxAmount)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return (false, "Auction not found.");
        if (!auction.IsActive || auction.EndTime <= DateTime.UtcNow) return (false, "This auction has ended.");
        if (auction.SellerId == userId) return (false, "You cannot set auto-bid on your own auction.");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");
        
        // We require the user to have at least the current minimum bid available
        decimal minRequired = auction.CurrentPrice + auction.MinIncrease;
        if (maxAmount < minRequired) return (false, $"Your maximum bid must be at least {minRequired:C}.");
        if (user.WalletBalance < minRequired) return (false, "Insufficient funds to start auto-bidding.");

        // Deactivate previous auto-bids for this user/auction
        var existingAutoBids = await _context.AutoBids
            .Where(ab => ab.AuctionId == auctionId && ab.UserId == userId && ab.IsActive)
            .ToListAsync();
        
        foreach (var oldAutoBid in existingAutoBids)
        {
            oldAutoBid.IsActive = false;
        }

        var autoBid = new AutoBid
        {
            AuctionId = auctionId,
            UserId = userId,
            MaxAmount = maxAmount,
            CreatedOn = DateTime.UtcNow,
            IsActive = true
        };

        _context.AutoBids.Add(autoBid);
        await _context.SaveChangesAsync();

        // If the user is not currently the winner, place an initial bid
        var currentWinnerId = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault()?.BidderId;
        if (currentWinnerId != userId)
        {
            return await PlaceBidAsync(auctionId, userId, minRequired);
        }

        return (true, "Auto-bidder activated successfully.");
    }

    public async Task<(bool Success, string Message)> PlaceBidAsync(int auctionId, string userId, decimal amount)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var auction = await _context.Auctions
                .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
                .FirstOrDefaultAsync(a => a.Id == auctionId);

            if (auction == null) return (false, "Auction not found.");

            // Validations
            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null) return (false, "User not found.");
            
            if (currentUser.IsShadowBanned)
            {
                // Silent failure or generic error
                return (false, "Your account has limited access. Please contact support.");
            }

            var userRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            var adminRoleId = (await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator"))?.Id;
            if (adminRoleId != null && userRoles.Any(ur => ur.RoleId == adminRoleId))
            {
                return (false, "Administrators are restricted from participating in auctions.");
            }

            if (auction.SellerId == userId) return (false, "You cannot bid on your own auction.");
            if (!auction.IsActive || auction.EndTime <= DateTime.UtcNow) return (false, "This auction has ended.");
            if (amount < auction.CurrentPrice + auction.MinIncrease) return (false, $"Bid must be at least {auction.CurrentPrice + auction.MinIncrease:C}.");

            if (currentUser.WalletBalance < amount) return (false, "Insufficient funds.");

            // 1. Charge User
            currentUser.WalletBalance -= amount;
            _context.Transactions.Add(new Transaction
            {
                UserId = userId,
                Amount = amount,
                Description = $"Bid on '{auction.Title}'",
                TransactionType = "Bid",
                TransactionDate = DateTime.UtcNow,
                AuctionId = auctionId
            });

            // 2. Refund Previous Bidder & Notify
            var previousHighBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (previousHighBid != null)
            {
                if (previousHighBid.BidderId == userId)
                {
                    currentUser.WalletBalance += previousHighBid.Amount;
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = userId,
                        Amount = previousHighBid.Amount,
                        Description = $"Refund outbid on '{auction.Title}'",
                        TransactionType = "Refund",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auctionId
                    });
                }
                else
                {
                    var previousBidder = previousHighBid.Bidder;
                    previousBidder.WalletBalance += previousHighBid.Amount;
                     _context.Transactions.Add(new Transaction
                    {
                        UserId = previousBidder.Id,
                        Amount = previousHighBid.Amount,
                        Description = $"Refund outbid on '{auction.Title}'",
                        TransactionType = "Refund",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auctionId
                    });

                    // NOTIFY PREVIOUS BIDDER
                    await _notificationService.NotifyUserAsync(previousBidder.Id, 
                        $"You have been outbid on '{auction.Title}'! Current price: {amount:C}", 
                        $"/Auctions/Details/{auctionId}");

                    // REAL-TIME SIGNALR NOTIFICATION
                    await _biddingNotificationService.NotifyOutbidAsync(previousBidder.Id, auctionId, auction.Title, amount);
                }
            }

            var bid = new Bid
            {
                AuctionId = auctionId,
                BidderId = userId,
                Amount = amount,
                BidTime = DateTime.UtcNow
            };

            auction.CurrentPrice = amount;
            auction.Bids.Add(bid);

            // ANTI-SNIPE: If bid is placed within last 2 minutes, extend by 2 minutes
            var timeToEnd = auction.EndTime - DateTime.UtcNow;
            if (timeToEnd.TotalMinutes < 2)
            {
                auction.EndTime = DateTime.UtcNow.AddMinutes(2);
                _logger.LogInformation($"Auction {auctionId} extended by 2 minutes due to late bid.");
            }

            // Notify SignalR for the MANUAL bid immediately
            await _biddingNotificationService.NotifyNewBidAsync(auctionId, currentUser.DisplayName ?? currentUser.UserName ?? "Unknown", amount, bid.BidTime);

            // NOTIFY WATCHERS (Only adds to context, doesn't save yet)
            await _notificationService.NotifyAllWatchersAsync(auctionId, 
                $"New bid on watched item '{auction.Title}': {amount:C}", 
                $"/Auctions/Details/{auctionId}",
                excludeUserId: userId);

            if (auction.BuyItNowPrice.HasValue && amount >= auction.BuyItNowPrice.Value)
            {
                auction.IsActive = false;
                auction.EndTime = DateTime.UtcNow;
                
                await _notificationService.NotifyAllWatchersAsync(auctionId, 
                    $"Auction '{auction.Title}' has ended (Buy It Now price reached).", 
                    $"/Auctions/Details/{auctionId}");
            }

            // --- Auto-Bidding Logic (Calculates everything in memory) ---
            await ProcessAutoBidsAsync(auction, userId);
            
            // ONE SINGLE SAVE FOR EVERYTHING
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return (true, "Bid placed successfully.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync();
            return (false, "Concurrency error: Someone else placed a bid. Please try again.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred while placing bid.");
        }
    }

    private async Task ProcessAutoBidsAsync(Auction auction, string lastBidderId)
    {
        // 1. Get ALL active auto-bids for this auction
        var allActiveAutoBids = await _context.AutoBids
            .Include(ab => ab.User)
            .Where(ab => ab.AuctionId == auction.Id && ab.IsActive)
            .ToListAsync();

        if (!allActiveAutoBids.Any()) return;

        // 2. Identify bots that are now disqualified (manual bid exceeded their MaxAmount)
        // We deactivate them so they don't try to bid again
        var disqualifiedBots = allActiveAutoBids
            .Where(ab => ab.MaxAmount < auction.CurrentPrice + auction.MinIncrease)
            .ToList();
        
        foreach (var bot in disqualifiedBots)
        {
            bot.IsActive = false;
        }

        // 3. Only consider bots that CAN still bid (have enough money AND MaxAmount is high enough)
        var validBots = allActiveAutoBids
            .Where(ab => ab.IsActive && 
                         ab.UserId != lastBidderId && 
                         ab.MaxAmount >= auction.CurrentPrice + auction.MinIncrease &&
                         ab.User.WalletBalance >= auction.CurrentPrice + auction.MinIncrease)
            .OrderByDescending(ab => ab.MaxAmount)
            .ThenBy(ab => ab.CreatedOn)
            .ToList();

        if (!validBots.Any()) return;

        // 4. The winner among bots is the one with the highest MaxAmount
        var winnerAutoBid = validBots.First();

        // 5. Calculate final price
        // It should be (highest challenger's limit + step) OR (manual bid + step)
        var challengers = allActiveAutoBids.Where(ab => ab.UserId != winnerAutoBid.UserId).ToList();
        decimal highestChallengerLimit = challengers.Any() ? challengers.Max(ab => ab.MaxAmount) : 0;
        
        // The price needs to beat both the manual bid AND any other bot's limit
        decimal baseToBeat = Math.Max(auction.CurrentPrice, highestChallengerLimit);
        decimal finalPrice = baseToBeat + auction.MinIncrease;

        // Cap the price at the winner's own limit
        if (finalPrice > winnerAutoBid.MaxAmount)
        {
            finalPrice = winnerAutoBid.MaxAmount;
        }

        // Final safety check: if for some reason the price didn't increase, force a step
        if (finalPrice <= auction.CurrentPrice)
        {
            finalPrice = auction.CurrentPrice + auction.MinIncrease;
        }

        // Ensure winner can still afford it
        var autoUser = winnerAutoBid.User;
        if (autoUser.WalletBalance < finalPrice)
        {
            winnerAutoBid.IsActive = false;
            return;
        }

        // A. Charge Winner
        autoUser.WalletBalance -= finalPrice;
        _context.Transactions.Add(new Transaction
        {
            UserId = autoUser.Id,
            Amount = finalPrice,
            Description = $"Auto-bid winner on '{auction.Title}'",
            TransactionType = "Auto-Bid",
            TransactionDate = DateTime.UtcNow
        });

        // B. Refund Previous High Bidder (the one from PlaceBidAsync)
        var prevBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (prevBid != null)
        {
            var prevUser = await _context.Users.FindAsync(prevBid.BidderId);
            if (prevUser != null)
            {
                prevUser.WalletBalance += prevBid.Amount;
                _context.Transactions.Add(new Transaction
                {
                    UserId = prevUser.Id,
                    Amount = prevBid.Amount,
                    Description = $"Refund (Auto-outbid) on '{auction.Title}'",
                    TransactionType = "Refund",
                    TransactionDate = DateTime.UtcNow
                });

                // Notify (Notifications will be saved when the main transaction commits)
                await _notificationService.NotifyUserAsync(prevUser.Id, 
                    $"An auto-bidder outbid you on '{auction.Title}'! Current price: {finalPrice:C}", 
                    $"/Auctions/Details/{auction.Id}");

                // REAL-TIME SIGNALR NOTIFICATION
                await _biddingNotificationService.NotifyOutbidAsync(prevUser.Id, auction.Id, auction.Title, finalPrice);
            }
        }

        // C. Create the Winning Bid
        var newBid = new Bid
        {
            AuctionId = auction.Id,
            BidderId = autoUser.Id,
            Amount = finalPrice,
            BidTime = DateTime.UtcNow
        };

        auction.CurrentPrice = finalPrice;
        auction.Bids.Add(newBid);

        // ANTI-SNIPE: Also apply to auto-bids
        var timeToEnd = auction.EndTime - DateTime.UtcNow;
        if (timeToEnd.TotalMinutes < 2)
        {
            auction.EndTime = DateTime.UtcNow.AddMinutes(2);
            _logger.LogInformation($"Auction {auction.Id} extended by 2 minutes due to auto-bid.");
        }

        // D. Deactivate bots that are now out of the race
        foreach (var bot in allActiveAutoBids.Where(ab => ab.MaxAmount < finalPrice + auction.MinIncrease))
        {
            bot.IsActive = false;
        }

        // 5. Final UI Sync (SignalR is usually safe to call, but we do it once)
        await _biddingNotificationService.NotifyNewBidAsync(auction.Id, autoUser.DisplayName ?? autoUser.UserName ?? "Auto-bidder", finalPrice, newBid.BidTime);
    }

    public async Task<(bool Success, string Message)> BuyItNowAsync(int auctionId, string userId)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var auction = await _context.Auctions
                .Include(a => a.Seller)
                .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
                .FirstOrDefaultAsync(a => a.Id == auctionId);

            if (auction == null) return (false, "Auction not found.");
            
            // Validation: Restrict Admin
            var userRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            var adminRoleId = (await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator"))?.Id;
            if (adminRoleId != null && userRoles.Any(ur => ur.RoleId == adminRoleId))
            {
                return (false, "Administrators are restricted from participating in auctions.");
            }

            if (!auction.BuyItNowPrice.HasValue) return (false, "This auction does not support 'Buy It Now'.");
            
            decimal price = auction.BuyItNowPrice.Value;

            if (auction.SellerId == userId) return (false, "You cannot buy your own item.");
            if (!auction.IsActive || auction.EndTime <= DateTime.UtcNow) return (false, "Auction ended.");

            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null || currentUser.WalletBalance < price) return (false, "Insufficient funds.");

            // 1. Charge Buyer
            currentUser.WalletBalance -= price;
            _context.Transactions.Add(new Transaction
            {
                UserId = userId,
                Amount = price,
                Description = $"Purchased '{auction.Title}' (Buy It Now)",
                TransactionType = "Purchase",
                TransactionDate = DateTime.UtcNow,
                AuctionId = auctionId
            });

            // 2. Log Escrow (Funds are held)
            _context.Transactions.Add(new Transaction
            {
                UserId = auction.SellerId,
                Amount = price,
                Description = $"Escrow: Payment for '{auction.Title}' (Buy It Now) held until delivery confirmation (Auction ID: {auctionId}).",
                TransactionType = "Escrow",
                TransactionDate = DateTime.UtcNow,
                AuctionId = auctionId
            });

            // 3. Refund Previous Bidder
            var previousHighBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (previousHighBid != null)
            {
                 if (previousHighBid.BidderId == userId)
                {
                     currentUser.WalletBalance += previousHighBid.Amount;
                     _context.Transactions.Add(new Transaction
                    {
                        UserId = userId,
                        Amount = previousHighBid.Amount,
                        Description = $"Refund (BIN upgrade) on '{auction.Title}'",
                        TransactionType = "Refund",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auctionId
                    });
                }
                else
                {
                    var previousBidder = previousHighBid.Bidder;
                    previousBidder.WalletBalance += previousHighBid.Amount;
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = previousBidder.Id,
                        Amount = previousHighBid.Amount,
                        Description = $"Refund (Item Sold) on '{auction.Title}'",
                        TransactionType = "Refund",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auctionId
                    });

                     // NOTIFY PREVIOUS BIDDER
                    await _notificationService.NotifyUserAsync(previousBidder.Id, 
                        $"Item '{auction.Title}' was purchased via Buy It Now. Your bid has been refunded.", 
                        $"/Auctions/Details/{auctionId}");
                }
            }

            // Create winning bid
            var bid = new Bid
            {
                AuctionId = auctionId,
                BidderId = userId,
                Amount = price,
                BidTime = DateTime.UtcNow
            };

            auction.CurrentPrice = price;
            auction.Bids.Add(bid);
            
            // Close Auction
            auction.IsActive = false;
            auction.EndTime = DateTime.UtcNow;

            // Deactivate all Auto-bids for this auction
            var activeBots = await _context.AutoBids
                .Where(ab => ab.AuctionId == auctionId && ab.IsActive)
                .ToListAsync();
            
            foreach (var bot in activeBots)
            {
                bot.IsActive = false;
            }

            // NOTIFY WATCHERS
            await _notificationService.NotifyAllWatchersAsync(auctionId, 
                $"Item '{auction.Title}' was sold for {price:C}!", 
                $"/Auctions/Details/{auctionId}",
                excludeUserId: userId);

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return (true, "Congratulations! You have purchased this item.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred during purchase.");
        }
    }
}
