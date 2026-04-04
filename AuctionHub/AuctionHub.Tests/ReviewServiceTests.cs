using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class ReviewServiceTests
{
    private AuctionHubDbContext GetDatabaseContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AuctionHubDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        
        var context = new AuctionHubDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task CanReviewAsync_ShouldReturnTrue_WhenWinnerReviewsSeller()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ReviewService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s", RowVersion = new byte[8] };
        var winner = new ApplicationUser { Id = "winner", UserName = "w", RowVersion = new byte[8] };
        var auction = new Auction 
        { 
            Id = 1, 
            SellerId = "seller", 
            IsActive = false, 
            EndTime = DateTime.UtcNow.AddDays(-1), 
            Description = "Test",
            RowVersion = new byte[8]
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m, BidTime = DateTime.UtcNow.AddHours(-2) });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        // Signature: CanReviewAsync(int auctionId, string userId)
        var result = await service.CanReviewAsync(1, "winner");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AddReviewAsync_ShouldSucceed_WhenValid()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ReviewService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s", RowVersion = new byte[8] };
        var winner = new ApplicationUser { Id = "winner", UserName = "w", RowVersion = new byte[8] };
        var auction = new Auction 
        { 
            Id = 1, 
            SellerId = "seller", 
            IsActive = false, 
            EndTime = DateTime.UtcNow.AddDays(-1),
            Description = "Test",
            RowVersion = new byte[8]
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m, BidTime = DateTime.UtcNow.AddHours(-2) });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        var model = new ReviewDto
        {
            AuctionId = 1,
            ReviewerId = "winner",
            Rating = 5,
            Comment = "Great seller!"
        };

        // Act
        // Signature: AddReviewAsync(ReviewDto model)
        var result = await service.AddReviewAsync(model);

        // Assert
        Assert.True(result);
        var review = await context.Reviews.FirstOrDefaultAsync();
        Assert.NotNull(review);
        Assert.Equal(5, review!.Rating);
    }
}
