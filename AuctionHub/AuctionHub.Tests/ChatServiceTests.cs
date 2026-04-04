using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
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
        // ... (existing test)
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ShouldHideForUserButShowForAdmin()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var admin = new ApplicationUser { Id = "admin1", UserName = "admin" };
        var adminRole = new Microsoft.AspNetCore.Identity.IdentityRole { Id = "role1", Name = "Administrator" };
        var user1 = new ApplicationUser { Id = "user1", UserName = "user1" };
        var user2 = new ApplicationUser { Id = "user2", UserName = "user2" };

        context.Users.AddRange(admin, user1, user2);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "admin1", RoleId = "role1" });

        var msg = new ChatMessage
        {
            Id = 1,
            AuctionId = 100,
            SenderId = "user1",
            ReceiverId = "admin1",
            Content = "Secret Message",
            IsGlobal = false,
            IsHiddenForReceiver = true // Admin hid this
        };
        context.ChatMessages.Add(msg);
        await context.SaveChangesAsync();

        // Act
        var messagesForUser = await service.GetPrivateMessagesAsync(100, "user1", "admin1"); // Admin as user1 (not admin role context here)
        var messagesForAdmin = await service.GetPrivateMessagesAsync(100, "admin1", "user1"); // Admin as admin1

        // Assert
        // Since GetPrivateMessagesAsync uses the first userId to check for admin role
        Assert.Single(messagesForAdmin); // Admin sees it even if hidden
        
        // Let's test a real non-admin user
        var msg2 = new ChatMessage
        {
            Id = 2,
            AuctionId = 100,
            SenderId = "user1",
            ReceiverId = "user2",
            Content = "Hidden from user2",
            IsGlobal = false,
            IsHiddenForReceiver = true
        };
        context.ChatMessages.Add(msg2);
        await context.SaveChangesAsync();

        var messagesForUser2 = await service.GetPrivateMessagesAsync(100, "user2", "user1");
        Assert.Empty(messagesForUser2); // user2 doesn't see it
    }
}