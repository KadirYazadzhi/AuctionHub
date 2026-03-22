using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuctionHub.Tests;

public class NotificationServiceTests
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
    public async Task NotifyUserAsync_ShouldCreateNotification()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new NotificationService(context);

        var user = new ApplicationUser { Id = "user1", UserName = "user1" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        await service.NotifyUserAsync("user1", "Hello World", "/test-link");

        // Assert
        var notification = await context.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal("user1", notification!.UserId);
        Assert.Equal("Hello World", notification.Message);
        Assert.Equal("/test-link", notification.Link);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldWork()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new NotificationService(context);

        var notification = new Notification 
        { 
            UserId = "user1", 
            Message = "Test", 
            IsRead = false, 
            CreatedOn = DateTime.UtcNow 
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // Act
        await service.MarkAsReadAsync(notification.Id, "user1");

        // Assert
        var updated = await context.Notifications.FirstAsync();
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task NotifyAllWatchersAsync_ShouldNotifyInterestedUsers()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new NotificationService(context);

        var user1 = new ApplicationUser { Id = "user1", UserName = "user1" };
        var user2 = new ApplicationUser { Id = "user2", UserName = "user2" };
        var user3 = new ApplicationUser { Id = "user3", UserName = "user3" };
        
        context.Users.AddRange(user1, user2, user3);
        context.Watchlist.AddRange(
            new AuctionWatchlist { AuctionId = 1, UserId = "user1" },
            new AuctionWatchlist { AuctionId = 1, UserId = "user2" },
            new AuctionWatchlist { AuctionId = 2, UserId = "user3" }
        );
        await context.SaveChangesAsync();

        // Act
        await service.NotifyAllWatchersAsync(1, "New bid on item 1", "/item/1", excludeUserId: "user1");

        // Assert
        var count = await context.Notifications.CountAsync();
        Assert.Equal(1, count); // Only user2 should be notified
        var notification = await context.Notifications.FirstAsync();
        Assert.Equal("user2", notification.UserId);
    }
}
