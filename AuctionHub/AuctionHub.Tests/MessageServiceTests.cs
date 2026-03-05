using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuctionHub.Tests;

public class MessageServiceTests
{
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public MessageServiceTests()
    {
        _mockEmailService = new Mock<IEmailService>();
        _mockNotificationService = new Mock<INotificationService>();
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
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
    public async Task CreateAsync_ShouldCreateMessage()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new MessageService(context, _mockEmailService.Object, _mockNotificationService.Object, _mockUserManager.Object);

        var model = new ContactMessageDto
        {
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Test Message"
        };

        // Act
        await service.CreateAsync(model);

        // Assert
        var message = await context.ContactMessages.FirstOrDefaultAsync();
        Assert.NotNull(message);
        Assert.Equal("John Doe", message.Name);
        Assert.Equal("john@example.com", message.Email);
        Assert.Equal("Test Message", message.Message);
    }

    [Fact]
    public async Task ReplyAsync_ShouldSendEmailAndNotification()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using var context = GetDatabaseContext(dbName);
        var service = new MessageService(context, _mockEmailService.Object, _mockNotificationService.Object, _mockUserManager.Object);

        var contactMsg = new ContactMessage
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com",
            Message = "Original Message"
        };
        context.ContactMessages.Add(contactMsg);
        
        var user = new ApplicationUser { Id = "user1", Email = "john@example.com" };
        _mockUserManager.Setup(m => m.FindByEmailAsync("john@example.com")).ReturnsAsync(user);
        
        await context.SaveChangesAsync();

        // Act
        var result = await service.ReplyAsync(1, "Our Response");

        // Assert
        Assert.True(result.Success);
        _mockEmailService.Verify(e => e.SendEmailAsync("john@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyUserAsync("user1", It.Is<string>(s => s.Contains("Our Response")), It.IsAny<string>()), Times.Once);
        
        var updatedMsg = await context.ContactMessages.FindAsync(1);
        Assert.True(updatedMsg!.IsRead);
    }
}