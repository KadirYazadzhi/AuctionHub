using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class AuctionService : IAuctionService
{
    private readonly IAuctionHubDbContext _context;
    private readonly INotificationService _notificationService;

    // In-memory cache to track auctions currently being created (prevents race conditions)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _inFlightAuctions 
        = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();

    public AuctionService(IAuctionHubDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
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
            IsSuspended = a.IsSuspended
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
            IsSuspended = a.IsSuspended
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
        
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var query = _context.Auctions
            .Include(a => a.Category)
            .Where(a => myBids.Any(b => b.AuctionId == a.Id) && !adminIds.Contains(a.SellerId));

        query = ApplyFilters(query, searchTerm, categoryId, minPrice, maxPrice, status);

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

        // Paginate before projection or after? Better after to get correct total count.
        // Actually, we need to project to get IsWinning.
        
        var list = await query.ToListAsync();
        var projectedList = list.Select(a => new AuctionDto
        {
            Id = a.Id,
            Title = a.Title,
            ImageUrl = a.ImageUrl,
            CurrentPrice = a.CurrentPrice,
            EndTime = a.EndTime,
            Category = a.Category.Name,
            CategoryId = a.CategoryId,
            IsActive = a.IsActive,
            IsSuspended = a.IsSuspended,
            IsWinning = myMaxBids.ContainsKey(a.Id) && myMaxBids[a.Id] >= a.CurrentPrice
        }).ToList();

        var totalCount = projectedList.Count;
        var items = projectedList.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PaginatedList<AuctionDto>(items, totalCount, pageNumber, pageSize);
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
            IsSuspended = a.IsSuspended
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
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        var adminIds = adminRole != null 
            ? await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync() 
            : new List<string>();

        var query = _context.Watchlist
            .Where(w => w.UserId == userId)
            .Include(w => w.Auction)
            .ThenInclude(a => a.Category)
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
            IsSuspended = a.IsSuspended
        });

        return await PaginatedList<AuctionDto>.CreateAsync(projectedQuery, pageNumber, pageSize);
    }

    public async Task<AuctionDetailsDto?> GetAuctionDetailsAsync(int id, string? currentUserId = null)
    {
        var auction = await _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .Include(a => a.Bids)
                .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return null;

        bool isWatched = false;
        if (currentUserId != null)
        {
            isWatched = await _context.Watchlist.AnyAsync(w => w.AuctionId == id && w.UserId == currentUserId);
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
            Seller = auction.Seller.DisplayName,
            SellerId = auction.SellerId,
            IsActive = auction.IsActive && auction.EndTime > DateTime.UtcNow,
            IsSuspended = auction.IsSuspended,
            IsWatched = isWatched,
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
        return await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();
    }

    public async Task<int> CreateAuctionAsync(AuctionFormDto model, string sellerId)
    {
        // 1. Generate a unique key for this specific submission
        string idempotencyKey = $"{sellerId}_{model.Title.Trim().ToLower()}";
        var now = DateTime.UtcNow;

        // 2. Clean up expired keys from the dictionary (older than 30 seconds)
        foreach (var key in _inFlightAuctions.Keys)
        {
            if (_inFlightAuctions.TryGetValue(key, out var timestamp) && (now - timestamp).TotalSeconds > 30)
            {
                _inFlightAuctions.TryRemove(key, out _);
            }
        }

        // 3. Try to "lock" this submission in memory
        if (!_inFlightAuctions.TryAdd(idempotencyKey, now))
        {
            // If we can't add it, it means an identical request is already processing or was just completed
            return -1;
        }

        try
        {
            // 4. Double check database just in case
            var recentThreshold = now.AddSeconds(-10);
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
                ImageUrl = model.ImageUrl,
                StartPrice = model.StartPrice,
                CurrentPrice = model.StartPrice,
                MinIncrease = model.MinIncrease,
                BuyItNowPrice = model.BuyItNowPrice,
                EndTime = new DateTime(model.EndTime.Year, model.EndTime.Month, model.EndTime.Day, 
                                     model.EndTime.Hour, model.EndTime.Minute, 0, 0, model.EndTime.Kind),
                CreatedOn = now,
                IsActive = true,
                CategoryId = model.CategoryId,
                SellerId = sellerId,
                RowVersion = new byte[8]
            };

            _context.Auctions.Add(auction);
            await _context.SaveChangesAsync();

            return auction.Id;
        }
        catch (Exception)
        {
            // Remove lock on failure so user can try again
            _inFlightAuctions.TryRemove(idempotencyKey, out _);
            throw;
        }
    }


    public async Task<(bool Success, string Message, string? OldImageUrl)> UpdateAuctionAsync(int id, AuctionFormDto model, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return (false, "Auction not found.", null);
        if (auction.SellerId != userId) return (false, "Forbidden.", null);
        if (auction.Bids.Any()) return (false, "You cannot edit an auction that has existing bids.", null);

        string? oldImageUrl = null;
        if (!string.IsNullOrEmpty(model.ImageUrl) && model.ImageUrl != auction.ImageUrl)
        {
            oldImageUrl = auction.ImageUrl;
            auction.ImageUrl = model.ImageUrl;
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
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return (false, "Auction not found.", null);
        if (auction.SellerId != userId) return (false, "Forbidden.", null);
        if (auction.Bids.Any()) return (false, "Cannot delete an auction that already has bids.", null);

        string? imageUrl = auction.ImageUrl;
        _context.Auctions.Remove(auction);
        await _context.SaveChangesAsync();

        return (true, "Auction deleted successfully.", imageUrl);
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
                TransactionDate = DateTime.UtcNow
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
                        TransactionDate = DateTime.UtcNow
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
                        TransactionDate = DateTime.UtcNow
                    });

                    // NOTIFY PREVIOUS BIDDER
                    await _notificationService.NotifyUserAsync(previousBidder.Id, 
                        $"You have been outbid on '{auction.Title}'! Current price: {amount:C}", 
                        $"/Auctions/Details/{auctionId}");
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

            // NOTIFY WATCHERS
            await _notificationService.NotifyAllWatchersAsync(auctionId, 
                $"New bid on watched item '{auction.Title}': {amount:C}", 
                $"/Auctions/Details/{auctionId}",
                excludeUserId: userId);

            if (auction.BuyItNowPrice.HasValue && amount >= auction.BuyItNowPrice.Value)
            {
                auction.IsActive = false;
                auction.EndTime = DateTime.UtcNow;
                
                // NOTIFY EVERYONE IT'S SOLD
                await _notificationService.NotifyAllWatchersAsync(auctionId, 
                    $"Auction '{auction.Title}' has ended (Buy It Now price reached).", 
                    $"/Auctions/Details/{auctionId}");
            }

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

    public async Task<(bool Success, string Message)> BuyItNowAsync(int auctionId, string userId)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var auction = await _context.Auctions
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

            // 1. Charge User
            currentUser.WalletBalance -= price;
            _context.Transactions.Add(new Transaction
            {
                UserId = userId,
                Amount = price,
                Description = $"Purchased '{auction.Title}' (Buy It Now)",
                TransactionType = "Purchase",
                TransactionDate = DateTime.UtcNow
            });

            // 2. Refund Previous Bidder
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
                        TransactionDate = DateTime.UtcNow
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
                        TransactionDate = DateTime.UtcNow
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
