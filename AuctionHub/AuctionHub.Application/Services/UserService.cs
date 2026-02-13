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
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return null;

        return MapToUserDetailsDto(user);
    }

    public async Task<UserDetailsDto?> GetByUsernameAsync(string username)
    {
        var user = await _context.Users
            .Include(u => u.MyAuctions).ThenInclude(a => a.Category)
            .Include(u => u.MyBids).ThenInclude(b => b.Auction)
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (user == null) return null;

        return MapToUserDetailsDto(user);
    }

    private UserDetailsDto MapToUserDetailsDto(ApplicationUser user)
    {
        return new UserDetailsDto
        {
            Id = user.Id,
            UserName = user.UserName ?? user.Email ?? "Unknown",
            Email = user.Email ?? "",
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            DisplayName = user.UserName ?? user.Email ?? "Unknown",
            WalletBalance = user.WalletBalance,
            LockoutEnd = user.LockoutEnd,
            Auctions = user.MyAuctions.Select(a => new AuctionDto
            {
                Id = a.Id,
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
}
