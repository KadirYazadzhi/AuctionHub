using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDetailsDto>> GetAllAsync(string? searchTerm);
    Task<UserDetailsDto?> GetByIdAsync(string id);
    Task<UserDetailsDto?> GetByUsernameAsync(string username);
    Task<(bool Success, string Message)> UpdateBalanceAsync(string userId, decimal amount, string reason);
    Task<(bool Success, string Message)> ToggleLockAsync(string userId);
}
