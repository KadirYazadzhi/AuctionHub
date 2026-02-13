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
    Task<(bool Success, string Message)> UpdateAuctionAsync(int id, AuctionFormDto model, string userId);
    Task<(bool Success, string Message)> DeleteAuctionAsync(int id, string userId);
    Task<(bool Success, string Message)> ToggleWatchlistAsync(int auctionId, string userId);
}
