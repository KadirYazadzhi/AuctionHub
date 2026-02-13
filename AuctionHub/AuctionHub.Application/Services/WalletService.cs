using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class WalletService : IWalletService
{
    private readonly IAuctionHubDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletService(IAuctionHubDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string userId)
    {
        return await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Description = t.Description,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                User = t.User.DisplayName
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionDto>> GetAllTransactionsAsync(int limit)
    {
        return await _context.Transactions
            .Include(t => t.User)
            .OrderByDescending(t => t.TransactionDate)
            .Take(limit)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Description = t.Description,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                User = t.User.DisplayName
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> AddFundsAsync(string userId, decimal amount)
    {
        if (amount <= 0) return (false, "Please enter a valid amount greater than 0.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return (false, "User not found.");

        user.WalletBalance += amount;
        
        var transaction = new Transaction
        {
            UserId = user.Id,
            Amount = amount,
            Description = "Deposit funds",
            TransactionType = "Deposit",
            TransactionDate = DateTime.UtcNow
        };
        _context.Transactions.Add(transaction);
        
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return (false, "Failed to update user wallet.");

        await _context.SaveChangesAsync();
        return (true, $"Successfully added {amount:C} to your wallet!");
    }
}
