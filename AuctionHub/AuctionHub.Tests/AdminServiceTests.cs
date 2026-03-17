using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuctionHub.Tests;

public class AdminServiceTests
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
    public async Task GetDashboardStatsAsync_ShouldCalculateRevenueCorrectly()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new AdminService(context);

        // Transactions
        context.Transactions.AddRange(
            new Transaction { TransactionType = "Promotion", Amount = 100m, TransactionDate = DateTime.UtcNow },
            new Transaction { TransactionType = "Commission", Amount = 50m, TransactionDate = DateTime.UtcNow },
            new Transaction { TransactionType = "AdminRefund", Amount = 20m, TransactionDate = DateTime.UtcNow }, // Deduct
            new Transaction { TransactionType = "Purchase", Amount = 500m, TransactionDate = DateTime.UtcNow } // Not revenue directly
        );

        // Users
        context.Users.AddRange(
            new ApplicationUser { Id = "u1", CreatedOn = DateTime.UtcNow },
            new ApplicationUser { Id = "u2", CreatedOn = DateTime.UtcNow.AddDays(-2) }
        );

        await context.SaveChangesAsync();

        // Act
        var stats = await service.GetDashboardStatsAsync();

        // Assert
        // (100 + 50) - 20 = 130
        Assert.Equal(130m, stats.TotalRevenue);
        Assert.Equal(2, stats.ActiveUsersCount);
        Assert.Equal(1, stats.NewUsersToday);
    }

    [Fact]
    public async Task ResolveDisputeAsync_Refund_ShouldReturnFundsToWinner()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new AdminService(context);

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 0m };
        var winner = new ApplicationUser { Id = "winner", WalletBalance = 100m };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "Disputed Item",
            SellerId = "seller",
            IsDisputed = true,
            IsSettled = false,
            CurrentPrice = 500m
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 500m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ResolveDisputeAsync(1, "Refund", "admin_id");

        // Assert
        Assert.True(result);
        var updatedWinner = await context.Users.FindAsync("winner");
        Assert.Equal(600m, updatedWinner!.WalletBalance); // 100 + 500 refund
        
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.True(updatedAuction!.IsSettled);
        Assert.False(updatedAuction.IsDisputed);
    }

    [Fact]
    public async Task ResolveDisputeAsync_Release_ShouldSendFundsToSeller()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new AdminService(context);

        var seller = new ApplicationUser { Id = "seller", WalletBalance = 0m };
        var winner = new ApplicationUser { Id = "winner", WalletBalance = 100m };
        
        var auction = new Auction
        {
            Id = 1,
            Title = "Disputed Item",
            SellerId = "seller",
            IsDisputed = true,
            IsSettled = false,
            CurrentPrice = 500m
        };
        auction.Bids.Add(new Bid { BidderId = "winner", Amount = 500m });

        context.Users.AddRange(seller, winner);
        context.Auctions.Add(auction);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ResolveDisputeAsync(1, "Release", "admin_id");

        // Assert
        Assert.True(result);
        var updatedSeller = await context.Users.FindAsync("seller");
        Assert.Equal(500m, updatedSeller!.WalletBalance);
        
        var updatedAuction = await context.Auctions.FindAsync(1);
        Assert.True(updatedAuction!.IsSettled);
        Assert.False(updatedAuction.IsDisputed);
    }

    [Fact]
    public async Task UpdateSystemSettingAsync_ShouldUpdateValue()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new AdminService(context);

        context.SystemSettings.Add(new SystemSetting { Key = "OldKey", Value = "OldValue" });
        await context.SaveChangesAsync();

        // Act
        var result = await service.UpdateSystemSettingAsync("OldKey", "NewValue", "admin_id");

        // Assert
        Assert.True(result);
        var setting = await context.SystemSettings.FirstAsync(s => s.Key == "OldKey");
        Assert.Equal("NewValue", setting.Value);
    }
}
