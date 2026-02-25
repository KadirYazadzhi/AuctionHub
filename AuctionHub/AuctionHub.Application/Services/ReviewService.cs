using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IAuctionHubDbContext _context;

    public ReviewService(IAuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message)> LeaveReviewAsync(int auctionId, string reviewerId, int rating, string comment)
    {
        if (!await CanLeaveReviewAsync(auctionId, reviewerId))
        {
            return (false, "You do not have permission to leave a review for this auction.");
        }

        var auction = await _context.Auctions.FindAsync(auctionId);
        if (auction == null) return (false, "Auction not found.");

        var review = new Review
        {
            AuctionId = auctionId,
            ReviewerId = reviewerId,
            TargetUserId = auction.SellerId,
            Rating = rating,
            Comment = comment.Trim(),
            CreatedOn = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return (true, "Your review has been submitted successfully.");
    }

    public async Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId)
    {
        return await _context.Reviews
            .Where(r => r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedOn)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                ReviewerName = r.Reviewer.FirstName != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : r.Reviewer.UserName!,
                ReviewerId = r.ReviewerId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedOn = r.CreatedOn,
                AuctionTitle = r.Auction.Title
            })
            .ToListAsync();
    }

    public async Task<bool> CanLeaveReviewAsync(int auctionId, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return false;

        // Auction must be closed
        if (auction.IsActive && auction.EndTime > DateTime.UtcNow) return false;

        // User must be the winner (highest bidder)
        var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (highestBid == null || highestBid.BidderId != userId) return false;

        // Check if review already exists
        var existingReview = await _context.Reviews
            .AnyAsync(r => r.AuctionId == auctionId && r.ReviewerId == userId);

        return !existingReview;
    }
}
