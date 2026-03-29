using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDetailsDto>> GetAllAsync(string? searchTerm);
    Task<PaginatedList<UserDetailsDto>> GetPaginatedAsync(string? searchTerm, int pageIndex, int pageSize);
    Task<UserDetailsDto?> GetByIdAsync(string id);
    Task<UserDetailsDto?> GetByUsernameAsync(string username);
    Task<UserDetailsDto?> GetByPublicIdAsync(Guid publicId);
    Task<(bool Success, string Message)> UpdateBalanceAsync(string userId, decimal amount, string reason);
    Task<(bool Success, string Message)> ToggleLockAsync(string userId);
    Task<(bool Success, string Message)> ToggleShadowBanAsync(string userId);

    // --- Social ---
    Task<(bool Success, string Message)> FollowUserAsync(string followerId, string sellerId);
    Task<(bool Success, string Message)> UnfollowUserAsync(string followerId, string sellerId);
    Task<bool> IsFollowingAsync(string followerId, string sellerId);
}
