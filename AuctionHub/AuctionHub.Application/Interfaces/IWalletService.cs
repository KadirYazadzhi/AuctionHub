using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IWalletService
{
    Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string userId);
    Task<IEnumerable<TransactionDto>> GetAllTransactionsAsync(int limit);
    Task<(bool Success, string Message)> AddFundsAsync(string userId, decimal amount);
}
