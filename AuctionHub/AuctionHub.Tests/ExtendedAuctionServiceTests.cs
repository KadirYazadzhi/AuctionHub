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

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 0m, RowVersion = new byte[8] };
        var bidder = new ApplicationUser { Id = "bidder", WalletBalance = 1000m, RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        // Set EndTime to 1 minute from now
        var originalEndTime = DateTime.UtcNow.AddMinutes(1);
        var auction = new Auction
        {
            Id = 1,
            Title = "Snipe Test",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = originalEndTime,
            StartPrice = 100m,
            CurrentPrice = 100m,
            MinIncrease = 10m,
            RowVersion = new byte[8]
        };

        context.Users.AddRange(seller, bidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(auction.Id, bidder.Id, 150m);

        // Assert
        Assert.True(result.Success);
        var updatedAuction = await context.Auctions.FindAsync(1);
        // Should be extended by 2 minutes from the time of bid
        Assert.True(updatedAuction!.EndTime > originalEndTime);
        Assert.True((updatedAuction.EndTime - DateTime.UtcNow).TotalMinutes <= 2.1);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldTriggerAutoBid_AndCalculateFinalPriceCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 0m, RowVersion = new byte[8] };
        var manualBidder = new ApplicationUser { Id = "manual", WalletBalance = 1000m, RowVersion = new byte[8] };
        var autoBidder = new ApplicationUser { Id = "bot", WalletBalance = 2000m, UserName = "AutoBot", RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "AutoBid Test",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = DateTime.UtcNow.AddDays(1),
            StartPrice = 100m,
            CurrentPrice = 100m,
            MinIncrease = 10m,
            RowVersion = new byte[8]
        };

        context.Users.AddRange(seller, manualBidder, autoBidder);
        context.Categories.Add(category);
        context.Auctions.Add(auction);

        // Set auto-bid for bot with max 500
        context.AutoBids.Add(new AutoBid { AuctionId = 1, UserId = "bot", MaxAmount = 500m, IsActive = true, CreatedOn = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Act: Manual user bids 150
        // Expected: Bot should automatically outbid them to 150 + 10 = 160
        var result = await service.PlaceBidAsync(1, "manual", 150m);

        // Assert
        Assert.True(result.Success);
        var updatedAuction = await context.Auctions.Include(a => a.Bids).FirstAsync(a => a.Id == 1);
        
        // Current price should be 160 (manual 150 + minIncrease 10)
        Assert.Equal(160m, updatedAuction.CurrentPrice);
        
        // Latest bid should belong to the bot
        var latestBid = updatedAuction.Bids.OrderByDescending(b => b.Amount).First();
        Assert.Equal("bot", latestBid.BidderId);
        Assert.Equal(160m, latestBid.Amount);

        // Bot balance should be 2000 - 160 = 1840
        var botUser = await context.Users.FindAsync("bot");
        Assert.Equal(1840m, botUser!.WalletBalance);

        // Manual user should have been refunded for their outbid bid
        var manualUser = await context.Users.FindAsync("manual");
        Assert.Equal(1000m, manualUser!.WalletBalance);
    }

    [Fact]
    public async Task BuyItNowAsync_ShouldCloseAuction_AndRefundHighBidder()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 0m, RowVersion = new byte[8] };
        var currentBidder = new ApplicationUser { Id = "bidder1", WalletBalance = 500m, RowVersion = new byte[8] };
        var buyer = new ApplicationUser { Id = "buyer", WalletBalance = 2000m, RowVersion = new byte[8] };
        var category = new Category { Id = 1, Name = "Test" };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "BIN Test",
            SellerId = "seller",
            CategoryId = 1,
            IsActive = true,
            EndTime = DateTime.UtcNow.AddDays(1),
            StartPrice = 100m,
            CurrentPrice = 300m,
            BuyItNowPrice = 1000m,
            MinIncrease = 10m,
            RowVersion = new byte[8]
        };
        // Add existing bid
        auction.Bids.Add(new Bid { BidderId = "bidder1", Amount = 300m, BidTime = DateTime.UtcNow.AddHours(-1) });

        context.Users.AddRange(seller, currentBidder, buyer);
        context.Categories.Add(category);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.BuyItNowAsync(1, "buyer");

        // Assert
        Assert.True(result.Success);
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.False(updatedAuction!.IsActive);
        Assert.Equal(1000m, updatedAuction.CurrentPrice);

        // Previous bidder should be refunded
        var refundedBidder = await context.Users.FindAsync("bidder1");
        Assert.Equal(800m, refundedBidder!.WalletBalance); // 500 original + 300 refund

        // Buyer balance
        var buyerUser = await context.Users.FindAsync("buyer");
        Assert.Equal(1000m, buyerUser!.WalletBalance); // 2000 - 1000
    }

    [Fact]
    public async Task CreateAuctionAsync_ShouldPreventDuplicates_Within30Seconds()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = CreateService(context);

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 100m, RowVersion = new byte[8] };
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
        Assert.Equal(-1, result2.AuctionId); // Second one should be flagged as duplicate
    }
}
