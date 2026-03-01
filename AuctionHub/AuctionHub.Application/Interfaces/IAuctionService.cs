using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IAuctionService
{
    Task<(bool Success, string Message)> PlaceBidAsync(int auctionId, string userId, decimal amount);
    Task<(bool Success, string Message)> BuyItNowAsync(int auctionId, string userId);
    Task<PaginatedList<AuctionDto>> GetAuctionsAsync(
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status,
        string? currentUserId = null);

    Task<PaginatedList<AuctionDto>> GetMyAuctionsAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status);

    Task<PaginatedList<AuctionDto>> GetMyBidsAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status);

    Task<PaginatedList<AuctionDto>> GetUserAuctionsAsync(
        string username,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status);

    Task<PaginatedList<AuctionDto>> GetMyWatchlistAsync(
        string userId,
        string? searchTerm, 
        int? categoryId, 
        string? sortOrder, 
        int pageNumber, 
        int pageSize, 
        decimal? minPrice, 
        decimal? maxPrice, 
        string? status);

    Task<AuctionDetailsDto?> GetAuctionDetailsAsync(int id, string? currentUserId = null);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<int> CreateAuctionAsync(AuctionFormDto model, string sellerId);
    Task<(bool Success, string Message, string? OldImageUrl)> UpdateAuctionAsync(int id, AuctionFormDto model, string userId);
    Task<(bool Success, string Message, IEnumerable<string>? ImageUrls)> DeleteAuctionAsync(int id, string userId);
    Task<(bool Success, string Message)> ToggleWatchlistAsync(int auctionId, string userId);
    Task<(bool Success, string Message)> SetAutoBidAsync(int auctionId, string userId, decimal maxAmount);
    Task<(bool Success, string Message)> ConfirmDeliveryAsync(int auctionId, string userId);
    Task<(bool Success, string Message)> PromoteAuctionAsync(int auctionId, string userId);
    Task<(bool Success, string Message)> ReportAuctionAsync(int auctionId, string userId, string reason, string details);
    Task<IEnumerable<AuctionDto>> GetEndingSoonAuctionsAsync(int count, string? currentUserId = null);
    Task<(bool Success, string Message)> CancelAuctionAsync(int auctionId, string userId);
    Task<(bool Success, string Message)> DeactivateAutoBidAsync(int auctionId, string userId);
    Task<(bool Success, string Message)> DisputeAuctionAsync(int auctionId, string userId);
}
