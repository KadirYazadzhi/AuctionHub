using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IReviewService
{
    Task<bool> AddReviewAsync(ReviewDto model);
    Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId);
    Task<bool> CanReviewAsync(int auctionId, string userId);
}
