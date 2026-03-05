using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class ChatServiceTests
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
    public async Task SaveMessageAsync_ShouldSaveGlobalMessage()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var sender = new ApplicationUser { Id = "user1", UserName = "sender" };
        context.Users.Add(sender);
        await context.SaveChangesAsync();

        // Act
        var result = await service.SaveMessageAsync("user1", "Hello World", isGlobal: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World", result.Content);
        Assert.True(result.IsGlobal);
        var message = await context.ChatMessages.FirstOrDefaultAsync();
        Assert.NotNull(message);
        Assert.True(message.IsGlobal);
    }

    [Fact]
    public async Task CanAccessPrivateChatAsync_ShouldAllowAdmin()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var admin = new ApplicationUser { Id = "admin1" };
        var adminRole = new IdentityRole { Id = "role1", Name = "Administrator" };
        context.Users.Add(admin);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRole.Id });
        await context.SaveChangesAsync();

        // Act
        var result = await service.CanAccessPrivateChatAsync(1, "admin1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanAccessPrivateChatAsync_ShouldAllowSellerAndWinner()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var seller = new ApplicationUser { Id = "seller" };
        var winner = new ApplicationUser { Id = "winner" };
        var auction = new Auction { Id = 1, SellerId = "seller", IsActive = false, EndTime = DateTime.UtcNow.AddHours(-1) };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 100m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act & Assert
        Assert.True(await service.CanAccessPrivateChatAsync(1, "seller"));
        Assert.True(await service.CanAccessPrivateChatAsync(1, "winner"));
        Assert.False(await service.CanAccessPrivateChatAsync(1, "other"));
    }
}