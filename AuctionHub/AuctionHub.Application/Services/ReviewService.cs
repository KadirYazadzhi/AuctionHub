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

    public async Task<bool> AddReviewAsync(ReviewDto model)
    {
        if (!await CanReviewAsync(model.AuctionId, model.ReviewerId))
        {
            return false;
        }

        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == model.AuctionId);

        if (auction == null) return false;

        // Security: Can only review within 30 days of auction end
        if (auction.EndTime.AddDays(30) < DateTime.UtcNow)
        {
            return false;
        }

        string targetUserId;
        if (model.ReviewerId == auction.SellerId)
        {
            // Seller is reviewing the buyer
            var winnerId = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault()?.BidderId;
            if (winnerId == null) return false;
            targetUserId = winnerId;
        }
        else
        {
            // Buyer is reviewing the seller
            targetUserId = auction.SellerId;
        }

        var review = new Review
        {
            AuctionId = model.AuctionId,
            ReviewerId = model.ReviewerId,
            TargetUserId = targetUserId,
            Rating = model.Rating,
            Comment = model.Comment.Trim(),
            CreatedOn = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<ReviewDto>> GetUserReviewsAsync(string userId)
    {
        return await _context.Reviews
            .Where(r => r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedOn)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                AuctionId = r.AuctionId,
                ReviewerName = r.Reviewer.FirstName != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : r.Reviewer.UserName!,
                ReviewerId = r.ReviewerId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedOn = r.CreatedOn,
                AuctionTitle = r.Auction.Title
            })
            .ToListAsync();
    }

    public async Task<bool> CanReviewAsync(int auctionId, string userId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return false;

        // Auction must be closed
        if (auction.IsActive && auction.EndTime > DateTime.UtcNow) return false;

        var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (highestBid == null) return false;

        // Either user is the winner reviewing the seller OR user is the seller reviewing the winner
        bool isWinner = highestBid.BidderId == userId;
        bool isSeller = auction.SellerId == userId;

        if (!isWinner && !isSeller) return false;

        // Check if THIS specific reviewer already reviewed THIS specific auction
        var existingReview = await _context.Reviews
            .AnyAsync(r => r.AuctionId == auctionId && r.ReviewerId == userId);

        return !existingReview;
    }
}
