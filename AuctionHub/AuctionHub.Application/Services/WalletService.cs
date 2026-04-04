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
                Status = t.Status,
                User = t.User.UserName ?? "Unknown"
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
                Status = t.Status,
                User = t.User.UserName ?? "Unknown"
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> AddFundsAsync(string userId, decimal amount)
    {
        if (amount <= 0) return (false, "Please enter a valid amount greater than 0.");

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
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
            if (!result.Succeeded) 
            {
                await dbTransaction.RollbackAsync();
                return (false, "Failed to update user wallet.");
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            return (true, $"Successfully added {amount:C} to your wallet!");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred while adding funds.");
        }
    }

    public async Task<(bool Success, string Message)> WithdrawAsync(string userId, decimal amount)
    {
        if (amount <= 0) return (false, "Please enter a valid amount greater than 0.");

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            ApplicationUser? user;

            if (_context.Database.IsInMemory())
            {
                user = await _context.Users.FindAsync(userId);
            }
            else
            {
                // Use UPDLOCK to prevent race conditions on wallet balance checks
                user = await _context.Users
                    .FromSqlInterpolated($"SELECT * FROM AspNetUsers WITH (UPDLOCK, ROWLOCK) WHERE Id = {userId}")
                    .FirstOrDefaultAsync();
            }

            if (user == null) return (false, "User not found.");
            if (user.WalletBalance < amount) return (false, "Insufficient funds.");

            // Logic: Deduct funds immediately but mark as Pending if large amount
            bool requiresApproval = amount >= 500;
            user.WalletBalance -= amount;
            
            var transaction = new Transaction
            {
                UserId = user.Id,
                Amount = -amount,
                Description = requiresApproval ? $"Withdraw funds (Awaiting Admin Approval)" : "Withdraw funds",
                TransactionType = "Withdrawal",
                Status = requiresApproval ? "Pending" : "Completed",
                TransactionDate = DateTime.UtcNow
            };
            _context.Transactions.Add(transaction);
            
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) 
            {
                await dbTransaction.RollbackAsync();
                return (false, "Failed to update user wallet.");
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            if (requiresApproval)
            {
                return (true, $"Your withdrawal request for {amount:C} is pending admin approval.");
            }
            return (true, $"Successfully withdrawn {amount:C} from your wallet!");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync();
            return (false, "An error occurred while withdrawing funds.");
        }
    }
}
