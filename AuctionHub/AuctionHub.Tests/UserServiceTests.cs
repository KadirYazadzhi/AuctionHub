using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class UserServiceTests
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
    public async Task GetUserDetailsAsync_ShouldCalculateRatingCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new UserService(context);

        var user = new ApplicationUser { Id = "user1", UserName = "tester", PublicId = Guid.NewGuid() };
        context.Users.Add(user);
        
        // Reviews for user1
        context.Reviews.AddRange(
            new Review { TargetUserId = "user1", Rating = 5, ReviewerId = "r1", CreatedOn = DateTime.UtcNow },
            new Review { TargetUserId = "user1", Rating = 3, ReviewerId = "r2", CreatedOn = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();

        // Act
        var result = await service.GetUserDetailsAsync(user.PublicId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4.0, result!.Rating); // (5 + 3) / 2
        Assert.Equal(2, result.ReviewCount);
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldChangeDisplayNameAndBio()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new UserService(context);

        var user = new ApplicationUser { Id = "user1", UserName = "old", DisplayName = "OldName", Bio = "OldBio" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var model = new UserDetailsDto { DisplayName = "NewName", Bio = "NewBio" };

        // Act
        var result = await service.UpdateProfileAsync("user1", model);

        // Assert
        Assert.True(result.Success);
        var updated = await context.Users.FindAsync("user1");
        Assert.Equal("NewName", updated!.DisplayName);
        Assert.Equal("NewBio", updated.Bio);
    }
}
