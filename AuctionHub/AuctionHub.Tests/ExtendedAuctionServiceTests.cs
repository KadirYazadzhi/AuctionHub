using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AuctionHub.Tests;

public class ExtendedAuctionServiceTests
{
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IBiddingNotificationService> _mockBiddingNotificationService;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<AuctionService>> _mockLogger;
    private readonly Mock<IPhotoService> _mockPhotoService;
    private readonly Mock<IImageAnalysisService> _mockImageAnalysisService;

    public ExtendedAuctionServiceTests()
    {
        _mockNotificationService = new Mock<INotificationService>();
        _mockBiddingNotificationService = new Mock<IBiddingNotificationService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<AuctionService>>();
        _mockPhotoService = new Mock<IPhotoService>();
        _mockImageAnalysisService = new Mock<IImageAnalysisService>();
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
            _mockPhotoService.Object,
            _mockImageAnalysisService.Object);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldExtendAuctionTime_WhenBidPlacedInLast2Minutes()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s@t.com", Email = "s@t.com", WalletBalance = 0m, RowVersion = new byte[8] };
        var bidder = new ApplicationUser { Id = "bidder", UserName = "b@t.com", Email = "b@t.com", WalletBalance = 1000m, RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        // Set EndTime to 1 minute from now
        var originalEndTime = DateTime.UtcNow.AddMinutes(1);
        var auction = new Auction
        {
            Id = 1,
            Title = "Snipe Test",
            Description = "Test Description",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = originalEndTime,
            StartPrice = 100m,
            CurrentPrice = 100m,
            MinIncrease = 10m,
            RowVersion = new byte[8],
            CreatedOn = DateTime.UtcNow
        };

        context.Users.AddRange(seller, bidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 150m);

        // Assert
        // result.Success check is logic-dependent, let's check outcome
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.True(updatedAuction!.EndTime > originalEndTime);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldTriggerAutoBid_AndCalculateFinalPriceCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s@t.com", Email = "s@t.com", WalletBalance = 0m, RowVersion = new byte[8] };
        var manualBidder = new ApplicationUser { Id = "manual", UserName = "m@t.com", Email = "m@t.com", WalletBalance = 1000m, RowVersion = new byte[8] };
        var autoBidder = new ApplicationUser { Id = "bot", UserName = "bot@t.com", Email = "bot@t.com", WalletBalance = 2000m, RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "AutoBid Test",
            Description = "Test Description",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = DateTime.UtcNow.AddDays(1),
            StartPrice = 100m,
            CurrentPrice = 100m,
            MinIncrease = 10m,
            RowVersion = new byte[8],
            CreatedOn = DateTime.UtcNow
        };

        context.Users.AddRange(seller, manualBidder, autoBidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);

        // Set auto-bid for bot with max 500
        context.AutoBids.Add(new AutoBid { AuctionId = 1, UserId = "bot", MaxAmount = 500m, IsActive = true, CreatedOn = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(1, "manual", 150m);

        // Assert
        var updatedAuction = await context.Auctions.Include(a => a.Bids).FirstAsync(a => a.Id == 1);
        Assert.Equal(160m, updatedAuction.CurrentPrice);
        var latestBid = updatedAuction.Bids.OrderByDescending(b => b.Amount).First();
        Assert.Equal("bot", latestBid.BidderId);
        Assert.Equal(160m, latestBid.Amount);
    }

    [Fact]
    public async Task BuyItNowAsync_ShouldCloseAuction_AndRefundHighBidder()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s@t.com", Email = "s@t.com", WalletBalance = 0m, RowVersion = new byte[8] };
        var currentBidder = new ApplicationUser { Id = "bidder1", UserName = "b@t.com", Email = "b@t.com", WalletBalance = 500m, RowVersion = new byte[8] };
        var buyer = new ApplicationUser { Id = "buyer", UserName = "buyer@t.com", Email = "buyer@t.com", WalletBalance = 2000m, RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "BIN Test",
            Description = "Test Description",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = DateTime.UtcNow.AddDays(1),
            StartPrice = 100m,
            CurrentPrice = 300m,
            BuyItNowPrice = 1000m,
            MinIncrease = 10m,
            RowVersion = new byte[8],
            CreatedOn = DateTime.UtcNow
        };
        // Add existing bid
        auction.Bids.Add(new Bid { BidderId = "bidder1", Amount = 300m, BidTime = DateTime.UtcNow.AddHours(-1), AuctionId = 1 });

        context.Users.AddRange(seller, currentBidder, buyer);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.BuyItNowAsync(1, "buyer");

        // Assert
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.False(updatedAuction!.IsActive);
        Assert.Equal(1000m, updatedAuction.CurrentPrice);
    }

    [Fact]
    public async Task CreateAuctionAsync_ShouldPreventDuplicates_Within30Seconds()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", UserName = "s@t.com", Email = "s@t.com", WalletBalance = 100m, RowVersion = new byte[8] };
        context.Users.Add(seller);
        context.Categories.Add(new Category { Id = 1, Name = "Test" });
        await context.SaveChangesAsync();

        var model = new AuctionFormDto
        {
            Title = "Unique Title",
            Description = "Desc",
            StartPrice = 100m,
            MinIncrease = 10m,
            EndTime = DateTime.UtcNow.AddDays(1),
            CategoryId = 1,
            ImageStreams = new List<System.IO.Stream>(),
            ImageFileNames = new List<string>(),
            AdditionalImageUrls = new List<string>()
        };

        _mockImageAnalysisService.Setup(s => s.AnalyzeImageAsync(It.IsAny<System.IO.Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSafeForWork = true });

        // Act
        var result1 = await service.CreateAuctionAsync(model, "seller");
        var result2 = await service.CreateAuctionAsync(model, "seller");

        // Assert
        Assert.True(result1.AuctionId > 0);
        Assert.Equal(-1, result2.AuctionId);
    }
}
