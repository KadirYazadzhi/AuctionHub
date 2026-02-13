using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.Services;
using AuctionHub.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class AuctionServiceTests
{
    private IAuctionHubDbContext GetDatabaseContext()
    {
        var options = new DbContextOptionsBuilder<AuctionHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        
        var context = new AuctionHubDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldPlaceBidSuccessfully_WhenValid()
    {
        // Arrange
        var context = GetDatabaseContext();
        var mockNotification = new Mock<INotificationService>();
        var service = new AuctionService(context, mockNotification.Object);

        var seller = new ApplicationUser { Id = "seller1", Email = "seller@test.com", UserName = "seller", RowVersion = new byte[8] };
        var bidder = new ApplicationUser { Id = "bidder1", Email = "bidder@test.com", UserName = "bidder", WalletBalance = 1000m, RowVersion = new byte[8] };
        
        var auction = new Auction 
        { 
            Id = 1, 
            Title = "Test Item", 
            Description = "Test Desc",
            CurrentPrice = 100m, 
            StartPrice = 100m,
            MinIncrease = 10m,
            SellerId = seller.Id,
            IsActive = true,
            EndTime = DateTime.UtcNow.AddDays(1),
            RowVersion = new byte[8]
        };

        context.Users.AddRange(seller, bidder);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(1, "bidder1", 120m);

        // Assert
        Assert.True(result.Success);
        
        var updatedAuction = await context.Auctions.Include(a => a.Bids).FirstAsync(a => a.Id == 1);
        Assert.Equal(120m, updatedAuction.CurrentPrice);
        
        var updatedBidder = await context.Users.FindAsync("bidder1");
        Assert.Equal(880m, updatedBidder!.WalletBalance);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldFail_WhenUserIsAdministrator()
    {
        // Arrange
        var context = GetDatabaseContext();
        var mockNotification = new Mock<INotificationService>();
        var service = new AuctionService(context, mockNotification.Object);

        var admin = new ApplicationUser { Id = "admin1", Email = "admin@test.com", UserName = "admin", RowVersion = new byte[8] };
        var adminRole = new Microsoft.AspNetCore.Identity.IdentityRole { Id = "role1", Name = "Administrator", NormalizedName = "ADMINISTRATOR" };
        
        context.Users.Add(admin);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "admin1", RoleId = "role1" });
        
        var auction = new Auction { Id = 1, Title = "Title", Description = "Desc", SellerId = "other", CurrentPrice = 100m, MinIncrease = 10m, IsActive = true, EndTime = DateTime.UtcNow.AddDays(1), RowVersion = new byte[8] };
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.PlaceBidAsync(1, "admin1", 200m);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Administrators are restricted", result.Message);
    }

    [Fact]
    public async Task PlaceBidAsync_ShouldNotifyPreviousBidder_AndWatchers()
    {
        // Arrange
        var context = GetDatabaseContext();
        var mockNotification = new Mock<INotificationService>();
        var service = new AuctionService(context, mockNotification.Object);

        var bidder1 = new ApplicationUser { Id = "bidder1", WalletBalance = 500m, RowVersion = new byte[8] };
        var bidder2 = new ApplicationUser { Id = "bidder2", WalletBalance = 1000m, RowVersion = new byte[8] };
        
        var auction = new Auction { Id = 1, Title = "Title", Description = "Desc", SellerId = "seller", CurrentPrice = 100m, MinIncrease = 10m, IsActive = true, EndTime = DateTime.UtcNow.AddDays(1), RowVersion = new byte[8] };
        auction.Bids.Add(new Bid { AuctionId = 1, BidderId = "bidder1", Amount = 100m });

        context.Users.AddRange(bidder1, bidder2);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        await service.PlaceBidAsync(1, "bidder2", 200m);

        // Assert
        // Check if NotifyUserAsync was called for the outbid user (bidder1)
        mockNotification.Verify(n => n.NotifyUserAsync("bidder1", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        
        // Check if NotifyAllWatchersAsync was called
        mockNotification.Verify(n => n.NotifyAllWatchersAsync(1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BuyItNowAsync_ShouldRefundAndCloseCorrecty()
    {
        // Arrange
        var context = GetDatabaseContext();
        var mockNotification = new Mock<INotificationService>();
        var service = new AuctionService(context, mockNotification.Object);

        var buyer = new ApplicationUser { Id = "buyer1", WalletBalance = 2000m, RowVersion = new byte[8] };
        var prevBidder = new ApplicationUser { Id = "prev1", WalletBalance = 500m, RowVersion = new byte[8] };
        
        var auction = new Auction 
        { 
            Id = 1, 
            Title = "Title",
            Description = "Desc",
            SellerId = "seller", 
            CurrentPrice = 500m, 
            BuyItNowPrice = 1500m, 
            IsActive = true, 
            EndTime = DateTime.UtcNow.AddDays(1),
            RowVersion = new byte[8]
        };
        auction.Bids.Add(new Bid { AuctionId = 1, BidderId = "prev1", Amount = 500m });

        context.Users.AddRange(buyer, prevBidder);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.BuyItNowAsync(1, "buyer1");

        // Assert
        Assert.True(result.Success);
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.False(updatedAuction!.IsActive);
        
        var updatedPrevBidder = await context.Users.FindAsync("prev1");
        Assert.Equal(1000m, updatedPrevBidder!.WalletBalance); // 500 original + 500 refund
        
        mockNotification.Verify(n => n.NotifyAllWatchersAsync(1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}