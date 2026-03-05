using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class WalletServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public WalletServiceTests()
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
    public async Task AddFundsAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new WalletService(context, _mockUserManager.Object);

        var user = new ApplicationUser { Id = "user1", WalletBalance = 100m };
        _mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Succeeded);

        // Act
        var result = await service.AddFundsAsync("user1", 50m);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(150m, user.WalletBalance);
        var transaction = await context.Transactions.FirstOrDefaultAsync(t => t.UserId == "user1");
        Assert.NotNull(transaction);
        Assert.Equal(50m, transaction.Amount);
        Assert.Equal("Deposit", transaction.TransactionType);
    }

    [Fact]
    public async Task WithdrawAsync_ShouldDecreaseBalance()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new WalletService(context, _mockUserManager.Object);

        var user = new ApplicationUser { Id = "user1", WalletBalance = 100m };
        _mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Succeeded);

        // Act
        var result = await service.WithdrawAsync("user1", 40m);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(60m, user.WalletBalance);
        var transaction = await context.Transactions.FirstOrDefaultAsync(t => t.UserId == "user1");
        Assert.NotNull(transaction);
        Assert.Equal(-40m, transaction.Amount);
        Assert.Equal("Withdrawal", transaction.TransactionType);
    }

    [Fact]
    public async Task WithdrawAsync_ShouldFail_WhenInsufficientFunds()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new WalletService(context, _mockUserManager.Object);

        var user = new ApplicationUser { Id = "user1", WalletBalance = 30m };
        _mockUserManager.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);

        // Act
        var result = await service.WithdrawAsync("user1", 50m);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Insufficient funds.", result.Message);
        Assert.Equal(30m, user.WalletBalance);
    }
}