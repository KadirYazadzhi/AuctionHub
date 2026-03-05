using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using System.Text;
using System.Text.Json;

namespace AuctionHub.Tests;

public class CategoryServiceTests
{
    private readonly Mock<IDistributedCache> _mockCache;

    public CategoryServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
    }

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
    public async Task GetAllAsync_ShouldReturnFromCache_IfAvailable()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        
        var cachedData = new List<CategoryDto> { new() { Id = 1, Name = "Cached" } };
        var json = JsonSerializer.Serialize(cachedData);
        _mockCache.Setup(c => c.GetAsync("Categories_List", default)).ReturnsAsync(Encoding.UTF8.GetBytes(json));

        var service = new CategoryService(context, _mockCache.Object);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached", result.First().Name);
        // Verify DB wasn't even touched for this call (implicit since we mock cache)
    }

    [Fact]
    public async Task CreateAsync_ShouldInvalidateCache()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new CategoryService(context, _mockCache.Object);

        var model = new CategoryDto { Name = "New Cat" };

        // Act
        await service.CreateAsync(model);

        // Assert
        var cat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "New Cat");
        Assert.NotNull(cat);
        _mockCache.Verify(c => c.RemoveAsync("Categories_List", default), Times.Once);
    }
}