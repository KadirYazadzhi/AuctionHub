using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IReviewService
{
    Task<(bool Success, string Message)> LeaveReviewAsync(int auctionId, string reviewerId, int rating, string comment);
    Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId);
    Task<bool> CanLeaveReviewAsync(int auctionId, string userId);
}
