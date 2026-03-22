using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuctionHub.Tests;

public class ReviewServiceTests
{
    private AuctionHubDbContext GetDatabaseContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AuctionHubDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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

        var seller = new ApplicationUser { Id = "seller", UserName = "seller" };
        var winner = new ApplicationUser { Id = "winner", UserName = "winner" };
        var auction = new Auction 
        { 
            Id = 1, 
            SellerId = "seller", 
            IsActive = false, 
            EndTime = DateTime.UtcNow.AddHours(-1) 
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.CanReviewAsync(1, "winner");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AddReviewAsync_ShouldFail_WhenAuctionIsStillActive()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ReviewService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "seller" };
        var winner = new ApplicationUser { Id = "winner", UserName = "winner" };
        var auction = new Auction 
        { 
            Id = 1, 
            SellerId = "seller", 
            IsActive = true, 
            EndTime = DateTime.UtcNow.AddHours(1) 
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        var reviewDto = new ReviewDto 
        { 
            AuctionId = 1, 
            ReviewerId = "winner", 
            Rating = 5, 
            Comment = "Great!" 
        };

        // Act
        var result = await service.AddReviewAsync(reviewDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AddReviewAsync_ShouldSucceed_WhenValid()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ReviewService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "seller" };
        var winner = new ApplicationUser { Id = "winner", UserName = "winner" };
        var auction = new Auction 
        { 
            Id = 1, 
            SellerId = "seller", 
            IsActive = false, 
            EndTime = DateTime.UtcNow.AddHours(-1) 
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        var reviewDto = new ReviewDto 
        { 
            AuctionId = 1, 
            ReviewerId = "winner", 
            Rating = 5, 
            Comment = "Excellent service!" 
        };

        // Act
        var result = await service.AddReviewAsync(reviewDto);

        // Assert
        Assert.True(result);
        var review = await context.Reviews.FirstOrDefaultAsync();
        Assert.NotNull(review);
        Assert.Equal("seller", review!.TargetUserId);
        Assert.Equal(5, review.Rating);
    }
}
