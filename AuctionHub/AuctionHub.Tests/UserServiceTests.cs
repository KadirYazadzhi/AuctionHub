using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public UserServiceTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
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

    [Fact]
    public async Task GetByPublicIdAsync_ShouldCalculateRatingCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new UserService(context, _mockUserManager.Object);

        var user = new ApplicationUser 
        { 
            Id = "user1", 
            UserName = "tester", 
            PublicId = Guid.NewGuid(),
            RowVersion = new byte[8]
        };
        context.Users.Add(user);
        
        // Reviews for user1
        context.Reviews.AddRange(
            new Review { TargetUserId = "user1", Rating = 5, ReviewerId = "r1", CreatedOn = DateTime.UtcNow, AuctionId = 1 },
            new Review { TargetUserId = "user1", Rating = 3, ReviewerId = "r2", CreatedOn = DateTime.UtcNow, AuctionId = 2 }
        );

        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByPublicIdAsync(user.PublicId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4.0, result!.AverageRating); // (5 + 3) / 2
        Assert.Equal(2, result.Reviews.Count);
    }
}
