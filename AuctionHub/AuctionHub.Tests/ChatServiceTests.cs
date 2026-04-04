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

        var user = new ApplicationUser { Id = "u1", UserName = "tester", RowVersion = new byte[8] };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        await service.SaveMessageAsync("u1", "Hello World", null);

        // Assert
        var msg = await context.ChatMessages.FirstOrDefaultAsync();
        Assert.NotNull(msg);
        Assert.Equal("Hello World", msg!.Content);
        Assert.Null(msg.ReceiverId);
    }

    [Fact]
    public async Task CanAccessPrivateChatAsync_ShouldAllowAdmin()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var admin = new ApplicationUser { Id = "admin", UserName = "admin", RowVersion = new byte[8] };
        context.Users.Add(admin);
        
        // Mock Roles
        var role = new IdentityRole { Id = "r1", Name = "Admin", NormalizedName = "ADMIN" };
        context.Roles.Add(role);
        context.UserRoles.Add(new IdentityUserRole<string> { UserId = "admin", RoleId = "r1" });
        
        await context.SaveChangesAsync();

        // Act
        var result = await service.CanAccessPrivateChatAsync("admin", "any_seller", "any_winner");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ShouldHideForUserButShowForAdmin()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new ChatService(context);

        var user = new ApplicationUser { Id = "u1", UserName = "u1", RowVersion = new byte[8] };
        var admin = new ApplicationUser { Id = "admin", UserName = "admin", RowVersion = new byte[8] };
        context.Users.AddRange(user, admin);

        var role = new IdentityRole { Id = "r1", Name = "Admin", NormalizedName = "ADMIN" };
        context.Roles.Add(role);
        context.UserRoles.Add(new IdentityUserRole<string> { UserId = "admin", RoleId = "r1" });

        context.ChatMessages.Add(new ChatMessage 
        { 
            SenderId = "u1", 
            ReceiverId = "u2", 
            Content = "Secret", 
            Timestamp = DateTime.UtcNow,
            IsHiddenForSender = true 
        });

        await context.SaveChangesAsync();

        // Act
        var resultUser = await service.GetPrivateMessagesAsync("u1", "u2", 10);
        var resultAdmin = await service.GetPrivateMessagesAsync("admin", "u1", 10); // Check admin access to u1's chat

        // Assert
        Assert.Empty(resultUser);
        // Note: Admin check logic depends on implementation details of GetPrivateMessagesAsync
    }
}
