using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class UserService : IUserService
{
    private readonly IAuctionHubDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(IAuctionHubDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IEnumerable<UserDetailsDto>> GetAllAsync(string? searchTerm)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(u => (u.Email != null && u.Email.Contains(searchTerm)) || 
                                     (u.UserName != null && u.UserName.Contains(searchTerm)) || 
                                     (u.FirstName != null && u.FirstName.Contains(searchTerm)) || 
                                     (u.LastName != null && u.LastName.Contains(searchTerm)));
        }

        return await query.Select(u => new UserDetailsDto
        {
            Id = u.Id,
            UserName = u.UserName ?? u.Email ?? "Unknown",
            Email = u.Email ?? "",
            FirstName = u.FirstName,
            LastName = u.LastName,
            ProfilePictureUrl = u.ProfilePictureUrl,
            AboutMe = u.AboutMe,
            DisplayName = u.UserName ?? u.Email ?? "Unknown", // Simplification
            WalletBalance = u.WalletBalance,
            LockoutEnd = u.LockoutEnd
        }).ToListAsync();
    }

    public async Task<UserDetailsDto?> GetByIdAsync(string id)
    {
        var user = await _context.Users
            .Include(u => u.MyAuctions).ThenInclude(a => a.Category)
            .Include(u => u.MyBids).ThenInclude(b => b.Auction)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Reviewer)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Auction)
            .Include(u => u.Followers).ThenInclude(f => f.Follower)
            .Include(u => u.Following).ThenInclude(f => f.Seller)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return null;

        return MapToUserDetailsDto(user);
    }

    public async Task<UserDetailsDto?> GetByUsernameAsync(string username)
    {
        var user = await _context.Users
            .Include(u => u.MyAuctions).ThenInclude(a => a.Category)
            .Include(u => u.MyBids).ThenInclude(b => b.Auction)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Reviewer)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Auction)
            .Include(u => u.Followers).ThenInclude(f => f.Follower)
            .Include(u => u.Following).ThenInclude(f => f.Seller)
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (user == null) return null;

        return MapToUserDetailsDto(user);
    }

    public async Task<UserDetailsDto?> GetByPublicIdAsync(Guid publicId)
    {
        var user = await _context.Users
            .Include(u => u.MyAuctions).ThenInclude(a => a.Category)
            .Include(u => u.MyBids).ThenInclude(b => b.Auction)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Reviewer)
            .Include(u => u.ReceivedReviews).ThenInclude(r => r.Auction)
            .Include(u => u.Followers).ThenInclude(f => f.Follower)
            .Include(u => u.Following).ThenInclude(f => f.Seller)
            .FirstOrDefaultAsync(u => u.PublicId == publicId);

        if (user == null) return null;

        return MapToUserDetailsDto(user);
    }

    public async Task<(bool Success, string Message)> ToggleShadowBanAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return (false, "User not found.");

        user.IsShadowBanned = !user.IsShadowBanned;
        await _context.SaveChangesAsync();

        string action = user.IsShadowBanned ? "Shadow-banned" : "Un-shadow-banned";
        return (true, $"User {user.UserName} has been {action}.");
    }

    private UserDetailsDto MapToUserDetailsDto(ApplicationUser user)
    {
        var transactions = _context.Transactions.Where(t => t.UserId == user.Id).ToList();
        
        var totalSpent = transactions.Where(t => t.TransactionType == "Purchase" || t.TransactionType == "Bid").Sum(t => t.Amount);
        var refunds = transactions.Where(t => t.TransactionType == "Refund").Sum(t => t.Amount);
        
        var totalEarned = transactions.Where(t => t.TransactionType == "Sale").Sum(t => t.Amount);
        
        var activeBidsCount = user.MyBids
            .Where(b => b.Auction.IsActive && b.Auction.EndTime > DateTime.UtcNow)
            .Select(b => b.AuctionId)
            .Distinct()
            .Count();

        // Win Rate Calculation
        var finishedAuctionsParticipated = user.MyBids
            .Where(b => !b.Auction.IsActive || b.Auction.EndTime <= DateTime.UtcNow)
            .Select(b => b.AuctionId)
            .Distinct()
            .Count();
        
        var wonCount = user.Transactions
            .Count(t => t.TransactionType == "Purchase" || t.TransactionType == "Escrow");

        double winRate = finishedAuctionsParticipated > 0 
            ? (double)wonCount / finishedAuctionsParticipated * 100 
            : 0;

        var personalActivity = user.MyBids
            .Where(b => b.BidTime >= DateTime.UtcNow.Date.AddDays(-7))
            .GroupBy(b => b.BidTime.Date)
            .Select(g => new DailyActivityDto { Date = g.Key, BidCount = g.Count() })
            .OrderBy(d => d.Date)
            .ToList();

        return new UserDetailsDto
        {
            Id = user.Id,
            PublicId = user.PublicId,
            UserName = user.UserName ?? user.Email ?? "Unknown",
            Email = user.Email ?? "",
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            AboutMe = user.AboutMe,
            DisplayName = user.UserName ?? user.Email ?? "Unknown",
            WalletBalance = user.WalletBalance,
            LockoutEnd = user.LockoutEnd,
            IsShadowBanned = user.IsShadowBanned,
            AverageRating = user.AverageRating,
            IsTopSeller = user.IsTopSeller,
            FollowersCount = user.Followers.Count,
            FollowingCount = user.Following.Count,

            Followers = user.Followers.Select(f => new FollowerDto
            {
                Id = f.FollowerId,
                PublicId = f.Follower.PublicId,
                DisplayName = f.Follower.DisplayName,
                ProfilePictureUrl = f.Follower.ProfilePictureUrl
            }).ToList(),

            Following = user.Following.Select(f => new FollowerDto
            {
                Id = f.SellerId,
                PublicId = f.Seller.PublicId,
                DisplayName = f.Seller.DisplayName,
                ProfilePictureUrl = f.Seller.ProfilePictureUrl
            }).ToList(),
            
            ActiveBidsCount = activeBidsCount,
            TotalSpent = totalSpent - refunds,
            TotalEarned = totalEarned,
            WinRate = Math.Round(winRate, 1),
            PersonalActivity = personalActivity,

            Reviews = user.ReceivedReviews.OrderByDescending(r => r.CreatedOn).Select(r => new ReviewDto
            {
                Id = r.Id,
                ReviewerName = r.Reviewer.FirstName != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : r.Reviewer.UserName!,
                ReviewerId = r.ReviewerId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedOn = r.CreatedOn,
                AuctionTitle = r.Auction.Title
            }).ToList(),
            Auctions = user.MyAuctions.Select(a => new AuctionDto
            {
                Id = a.Id,
                PublicId = a.PublicId,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category.Name,
                IsActive = a.IsActive
            }).ToList(),
            Bids = user.MyBids.Select(b => new BidDto
            {
                Amount = b.Amount,
                BidTime = b.BidTime,
                Bidder = user.UserName ?? user.Email ?? "Unknown",
                AuctionTitle = b.Auction.Title
            }).ToList()
        };
    }

    public async Task<(bool Success, string Message)> UpdateBalanceAsync(string userId, decimal amount, string reason)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return (false, "User not found.");

            // Validate that final balance won't be negative
            if (user.WalletBalance + amount < 0)
            {
                return (false, $"Cannot apply {amount:C}. Would result in negative balance. Current balance: {user.WalletBalance:C}");
            }

            user.WalletBalance += amount;
            
            // Force update of RowVersion
            _context.Entry(user).Property(u => u.RowVersion).IsModified = true;
            
            _context.Transactions.Add(new Transaction
            {
                UserId = user.Id,
                Amount = amount,
                TransactionType = amount >= 0 ? "AdminBonus" : "AdminPenalty",
                Description = string.IsNullOrEmpty(reason) ? "Administrative adjustment" : reason,
                TransactionDate = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return (true, $"Balance updated. New balance: {user.WalletBalance:C}");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return (false, "Concurrency error: The user's balance was modified by another process. Please try again.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return (false, "An error occurred while updating the balance.");
        }
    }

    public async Task<(bool Success, string Message)> ToggleLockAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return (false, "User not found.");

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            return (true, "User unlocked successfully.");
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            return (true, "User locked indefinitely.");
        }
    }

    // --- Social ---

    public async Task<(bool Success, string Message)> FollowUserAsync(string followerId, string sellerId)
    {
        if (followerId == sellerId) return (false, "You cannot follow yourself.");

        var existing = await _context.UserFollowers
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.SellerId == sellerId);

        if (existing != null) return (false, "You are already following this user.");

        var follower = new UserFollower
        {
            FollowerId = followerId,
            SellerId = sellerId,
            FollowedOn = DateTime.UtcNow
        };

        _context.UserFollowers.Add(follower);
        await _context.SaveChangesAsync();

        return (true, "User followed successfully.");
    }

    public async Task<(bool Success, string Message)> UnfollowUserAsync(string followerId, string sellerId)
    {
        var existing = await _context.UserFollowers
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.SellerId == sellerId);

        if (existing == null) return (false, "You are not following this user.");

        _context.UserFollowers.Remove(existing);
        await _context.SaveChangesAsync();

        return (true, "User unfollowed.");
    }

    public async Task<bool> IsFollowingAsync(string followerId, string sellerId)
    {
        return await _context.UserFollowers
            .AnyAsync(f => f.FollowerId == followerId && f.SellerId == sellerId);
    }
}
