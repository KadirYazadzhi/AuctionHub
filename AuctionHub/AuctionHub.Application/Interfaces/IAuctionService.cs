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
        string? currentUserId = null,
        double? latitude = null,
        double? longitude = null,
        double? maxDistance = null);

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
    Task<AuctionDetailsDto?> GetAuctionDetailsByPublicIdAsync(Guid publicId, string? currentUserId = null);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<(int AuctionId, string Message)> CreateAuctionAsync(AuctionFormDto model, string sellerId);
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
    Task<SellerAnalyticsDto> GetSellerAnalyticsAsync(string userId);
    
    // --- Private Offers ---
    Task<(bool Success, string Message)> MakePrivateOfferAsync(int auctionId, string buyerId, decimal amount);
    Task<(bool Success, string Message)> AcceptPrivateOfferAsync(int offerId, string sellerId);
    Task<(bool Success, string Message)> RejectPrivateOfferAsync(int offerId, string sellerId);
    Task<(bool Success, string Message)> PayParticipationFeeAsync(int auctionId, string userId);
    
    // --- Comments ---
    Task<(bool Success, string Message, CommentDto? Comment)> AddCommentAsync(int auctionId, string userId, string content);
    Task<IEnumerable<CommentDto>> GetCommentsAsync(int auctionId);

    // --- Background Jobs ---
    Task CloseExpiredAuctionsAsync();
    Task ReleaseEscrowFundsAsync();
    Task ProcessDutchAuctionsAsync();
}
