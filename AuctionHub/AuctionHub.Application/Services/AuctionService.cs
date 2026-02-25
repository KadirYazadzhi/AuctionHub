using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class AuctionService : IAuctionService
{
    private readonly IAuctionHubDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IBiddingNotificationService _biddingNotificationService;

    public AuctionService(IAuctionHubDbContext context, INotificationService notificationService, IBiddingNotificationService biddingNotificationService)
    {
        _context = context;
        _notificationService = notificationService;
        _biddingNotificationService = biddingNotificationService;
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
            Seller = auction.Seller.DisplayName,
            SellerId = auction.SellerId,
            IsActive = auction.IsActive && auction.EndTime > DateTime.UtcNow,
            IsSuspended = auction.IsSuspended,
            IsWatched = isWatched,
            IsWinning = currentUserId != null && auction.Bids.Any() && auction.Bids.OrderByDescending(b => b.Amount).First().BidderId == currentUserId,
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
        // 1. Get ALL active auto-bids that have enough money to at least place one more bid
        decimal minStep = auction.CurrentPrice + auction.MinIncrease;
        
        var activeAutoBids = await _context.AutoBids
            .Include(ab => ab.User)
            .Where(ab => ab.AuctionId == auction.Id && 
                         ab.IsActive && 
                         ab.User.WalletBalance >= auction.CurrentPrice + auction.MinIncrease)
            .OrderByDescending(ab => ab.MaxAmount)
            .ThenBy(ab => ab.CreatedOn)
            .ToListAsync();

        if (!activeAutoBids.Any()) return;

        // 2. Identify the winner among bots (must not be the person who just placed the manual bid)
        var winnerAutoBid = activeAutoBids.FirstOrDefault(ab => ab.UserId != lastBidderId);
        if (winnerAutoBid == null) return;

        // 3. Calculate final price based on the second-best challenger
        // Challenger can be another bot OR the current manual bidder's price
        var otherBots = activeAutoBids.Where(ab => ab.Id != winnerAutoBid.Id).ToList();
        var secondBestMax = otherBots.Any() ? otherBots.Max(ab => ab.MaxAmount) : auction.CurrentPrice;

        decimal finalPrice = secondBestMax + auction.MinIncrease;

        // Ensure we don't exceed the winner's own limit
        if (finalPrice > winnerAutoBid.MaxAmount)
        {
            finalPrice = winnerAutoBid.MaxAmount;
        }

        // Final check: Is the calculated final price actually higher than current?
        if (finalPrice <= auction.CurrentPrice)
        {
            finalPrice = auction.CurrentPrice + auction.MinIncrease;
        }

        // 4. Update Entities (NO SaveChangesAsync here!)
        var autoUser = winnerAutoBid.User;

        // Ensure user can still afford the calculated final price
        if (autoUser.WalletBalance < finalPrice)
        {
            // If they can't afford the jump, they just bid what they have or lose
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

        // D. Deactivate bots that are now out of the race
        foreach (var bot in activeAutoBids.Where(ab => ab.MaxAmount < finalPrice + auction.MinIncrease))
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
                TransactionDate = DateTime.UtcNow
            });

            // 2. Credit Seller
            auction.Seller.WalletBalance += price;
            _context.Transactions.Add(new Transaction
            {
                UserId = auction.SellerId,
                Amount = price,
                Description = $"Sale of item '{auction.Title}' (Buy It Now)",
                TransactionType = "Sale",
                TransactionDate = DateTime.UtcNow
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
