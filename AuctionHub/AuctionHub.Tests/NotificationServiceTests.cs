using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class NotificationServiceTests
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
    public async Task NotifyUserAsync_ShouldCreateNotification()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new NotificationService(context);

        var user = new ApplicationUser { Id = "u1", UserName = "u1@t.com", Email = "u1@t.com", RowVersion = new byte[8] };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        await service.NotifyUserAsync("u1", "Message", "/link");

        // Assert
        var notification = await context.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal("Message", notification!.Message);
    }

    [Fact]
    public async Task NotifyAllWatchersAsync_ShouldNotifyInterestedUsers()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new NotificationService(context);

        var u1 = new ApplicationUser { Id = "u1", UserName = "u1", RowVersion = new byte[8] };
        var u2 = new ApplicationUser { Id = "u2", UserName = "u2", RowVersion = new byte[8] };
        context.Users.AddRange(u1, u2);
        
        // Add auction
        context.Auctions.Add(new Auction 
        { 
            Id = 1, 
            Title = "T", 
            Description = "D", 
            SellerId = "u1", 
            CategoryId = 1, 
            CreatedOn = DateTime.UtcNow, 
            EndTime = DateTime.UtcNow.AddDays(1),
            RowVersion = new byte[8]
        });

        context.Watchlist.AddRange(
            new AuctionWatchlist { UserId = "u1", AuctionId = 1 },
            new AuctionWatchlist { UserId = "u2", AuctionId = 1 }
        );
        await context.SaveChangesAsync();

        // Act
        await service.NotifyAllWatchersAsync(1, "New Bid", "/link");

        // Assert
        var count = await context.Notifications.CountAsync();
        Assert.Equal(2, count);
    }
}
