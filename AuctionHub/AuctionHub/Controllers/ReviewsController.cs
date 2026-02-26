using System.Security.Claims;
using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Controllers;

[Authorize]
public class ReviewsController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly IAuctionService _auctionService;

    public ReviewsController(IReviewService reviewService, IAuctionService auctionService)
    {
        _reviewService = reviewService;
        _auctionService = auctionService;
    }

    [HttpGet]
    public async Task<IActionResult> LeaveReview(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var auction = await _auctionService.GetAuctionDetailsAsync(auctionId, currentUserId);
        if (auction == null) return NotFound();

        // Check if user is the winner
        if (auction.WinnerId != currentUserId)
        {
            TempData["Error"] = "Only the auction winner can leave a review.";
            return RedirectToAction("Details", "Auctions", new { id = auctionId });
        }

        // Check if already reviewed
        var canReview = await _reviewService.CanReviewAsync(auctionId, currentUserId!);
        if (!canReview)
        {
            TempData["Error"] = "You have already left a review for this auction.";
            return RedirectToAction("Details", "Auctions", new { id = auctionId });
        }

        var model = new ReviewDto
        {
            AuctionId = auctionId,
            AuctionTitle = auction.Title,
            ReviewerId = currentUserId!
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeaveReview(ReviewDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        model.ReviewerId = currentUserId!;

        var success = await _reviewService.AddReviewAsync(model);
        if (success)
        {
            TempData["Success"] = "Thank you for your feedback!";
            return RedirectToAction("Details", "Auctions", new { id = model.AuctionId });
        }

        TempData["Error"] = "Could not submit review. Please ensure you are the winner and the auction is closed.";
        return View(model);
    }
}
