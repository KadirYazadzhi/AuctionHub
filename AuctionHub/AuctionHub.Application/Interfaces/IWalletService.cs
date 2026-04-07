using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;

namespace AuctionHub.Application.Interfaces;

public interface IWalletService
{
    Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string userId);
    Task<IEnumerable<TransactionDto>> GetAllTransactionsAsync(int limit);
    Task<PaginatedList<TransactionDto>> GetPaginatedTransactionsAsync(int pageIndex, int pageSize);
    Task<(bool Success, string Message)> AddFundsAsync(string userId, decimal amount);
    Task<(bool Success, string Message)> WithdrawAsync(string userId, decimal amount);
}
