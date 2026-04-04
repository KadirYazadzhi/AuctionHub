using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
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
        var result = await service.CanReviewAsync("winner", 1);

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

        // Act
        var result = await service.AddReviewAsync(1, "winner", 5, "Great seller!");

        // Assert
        Assert.True(result.Success);
        var review = await context.Reviews.FirstOrDefaultAsync();
        Assert.NotNull(review);
        Assert.Equal(5, review!.Rating);
    }
}
