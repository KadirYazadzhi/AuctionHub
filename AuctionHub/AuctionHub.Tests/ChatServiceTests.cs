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
        await service.SaveMessageAsync("u1", "Hello World", true);

        // Assert
        var msg = await context.ChatMessages.FirstOrDefaultAsync();
        Assert.NotNull(msg);
        Assert.Equal("Hello World", msg!.Content);
        Assert.Null(msg.ReceiverId);
        Assert.True(msg.IsGlobal);
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
        // Signature: CanAccessPrivateChatAsync(int auctionId, string userId)
        var result = await service.CanAccessPrivateChatAsync(1, "admin");

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
            AuctionId = 1,
            Content = "Secret", 
            SentOn = DateTime.UtcNow,
            IsGlobal = false,
            IsHiddenForSender = true 
        });

        await context.SaveChangesAsync();

        // Act
        // Signature: GetPrivateMessagesAsync(int auctionId, string userId1, string userId2)
        var resultUser = await service.GetPrivateMessagesAsync(1, "u1", "u2");
        var resultAdmin = await service.GetPrivateMessagesAsync(1, "u1", "u2"); 

        // Assert
        Assert.Empty(resultUser);
        Assert.Single(resultAdmin);
    }
}
