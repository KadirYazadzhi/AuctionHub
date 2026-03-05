using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AuctionHub.Application.DTOs;

namespace AuctionHub.Tests;

public class AuctionServiceTests
{
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IBiddingNotificationService> _mockBiddingNotificationService;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<AuctionService>> _mockLogger;
    private readonly Mock<IPhotoService> _mockPhotoService;

    public AuctionServiceTests()
    {
        _mockNotificationService = new Mock<INotificationService>();
        _mockBiddingNotificationService = new Mock<IBiddingNotificationService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<AuctionService>>();
        _mockPhotoService = new Mock<IPhotoService>();
    }

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

    private AuctionService CreateService(AuctionHubDbContext context)
    {
        return new AuctionService(
            context, 
            _mockNotificationService.Object, 
            _mockBiddingNotificationService.Object,
            _mockCache.Object,
            _mockLogger.Object,
            _mockPhotoService.Object);
    }

    private static ApplicationUser CreateUser(string id, decimal wallet = 1000m) => new()
    {
        Id = id,
        UserName = $"{id}@test.com",
        Email = $"{id}@test.com",
        WalletBalance = wallet,
        RowVersion = new byte[8]
    };

    private static Category CreateCategory(int id = 1) => new() { Id = id, Name = "Test Category" };

    private static Auction CreateAuction(string sellerId, int categoryId, bool isActive = true, decimal currentPrice = 100m) => new()
    {
        Id = 1,
        Title = "Test Auction",
        Description = "Test Description",
        SellerId = sellerId,
        CategoryId = categoryId,
        IsActive = isActive,
        EndTime = isActive ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow.AddDays(-1),
        StartPrice = 100m,
        CurrentPrice = currentPrice,
        MinIncrease = 10m,
        BuyItNowPrice = 500m,
        RowVersion = new byte[8],
        CreatedOn = DateTime.UtcNow
    };

    [Fact]
    public async Task PlaceBidAsync_ShouldPlaceBidSuccessfully()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = CreateUser("seller");
        var bidder = CreateUser("bidder");
        var category = CreateCategory();
        var auction = CreateAuction(seller.Id, category.Id);

        context.Users.AddRange(seller, bidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 120m);

        // Assert
        Assert.True(result.Success);
        var updatedAuction = await context.Auctions.FindAsync(auction.Id);
        Assert.Equal(120m, updatedAuction!.CurrentPrice);
        var updatedBidder = await context.Users.FindAsync(bidder.Id);
        Assert.Equal(880m, updatedBidder!.WalletBalance);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldFail_WhenYouAreAlreadyHighestBidder()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = CreateUser("seller");
        var bidder = CreateUser("bidder");
        var category = CreateCategory();
        var auction = CreateAuction(seller.Id, category.Id);
        auction.Bids.Add(new Bid { BidderId = bidder.Id, Amount = 110m });
        auction.CurrentPrice = 110m;

        context.Users.AddRange(seller, bidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 130m);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You are already the highest bidder.", result.Message);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_ShouldCalculateCommissionCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = CreateUser("seller", wallet: 0m);
        var winner = CreateUser("winner");
        var admin = CreateUser("admin_system");
        admin.Email = "admin@auctionhub.com";
        
        var category = CreateCategory();
        var auction = CreateAuction(seller.Id, category.Id, isActive: false);
        auction.EndTime = DateTime.UtcNow.AddHours(-1);
        auction.Bids.Add(new Bid { BidderId = winner.Id, Amount = 1000m }); // Sale price 1000
        
        context.Users.AddRange(seller, winner, admin);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        
        // Seed 10% commission rate
        context.SystemSettings.Add(new SystemSetting { Key = "CommissionRate", Value = "10" });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ConfirmDeliveryAsync(auction.Id, winner.Id);

        // Assert
        Assert.True(result.Success);
        var updatedSeller = await context.Users.FindAsync(seller.Id);
        // 1000 - 10% (100) = 900
        Assert.Equal(900m, updatedSeller!.WalletBalance);
        
        var adminAccount = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@auctionhub.com");
        var commissionTx = await context.Transactions.FirstOrDefaultAsync(t => t.TransactionType == "Commission");
        Assert.NotNull(commissionTx);
        Assert.Equal(100m, commissionTx.Amount);
    }

    [Fact]
    public async Task DeactivateAutoBidAsync_ShouldWork()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var user = CreateUser("user1");
        var autobid = new AutoBid { AuctionId = 1, UserId = "user1", MaxAmount = 500m, IsActive = true };
        
        context.Users.Add(user);
        context.AutoBids.Add(autobid);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeactivateAutoBidAsync(1, "user1");

        // Assert
        Assert.True(result.Success);
        var updated = await context.AutoBids.FirstAsync();
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task CancelAuctionAsync_ShouldFail_IfBidsExist()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = CreateUser("seller");
        var category = CreateCategory();
        var auction = CreateAuction(seller.Id, category.Id);
        auction.Bids.Add(new Bid { BidderId = "bidder", Amount = 150m });

        context.Users.Add(seller);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.CancelAuctionAsync(auction.Id, seller.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You cannot cancel an auction that has bids.", result.Message);
    }
}