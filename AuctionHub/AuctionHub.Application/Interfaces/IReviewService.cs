using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IReviewService
{
    Task<(bool Success, string Message)> LeaveReviewAsync(int auctionId, string reviewerId, int rating, string comment);
    Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId);
    Task<bool> CanLeaveReviewAsync(int auctionId, string userId);
}

public class ReviewDto
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = null!;
    public string ReviewerId { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
    public string AuctionTitle { get; set; } = null!;
}
