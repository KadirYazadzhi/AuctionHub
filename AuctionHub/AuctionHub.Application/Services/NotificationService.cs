using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionHub.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task NotifyUserAsync(string userId, string message, string? link = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            Link = link,
            CreatedOn = DateTime.UtcNow,
            IsRead = false
        });

        await context.SaveChangesAsync();
    }

    public async Task NotifyAllUsersAsync(string message, string? link = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();
        
        // This can be heavy for thousands of users, better to use SignalR or separate table for global announcements.
        // For this project scale, creating records is acceptable.
        var userIds = await context.Users.Select(u => u.Id).ToListAsync();
        var notifications = new List<Notification>();

        foreach (var userId in userIds)
        {
            notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                Link = link,
                CreatedOn = DateTime.UtcNow,
                IsRead = false
            });
        }

        await context.Notifications.AddRangeAsync(notifications);
        await context.SaveChangesAsync();
    }

    public async Task NotifyAllWatchersAsync(int auctionId, string message, string? link = null, string? excludeUserId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        var watchers = await context.Watchlist
            .Where(w => w.AuctionId == auctionId)
            .Select(w => w.UserId)
            .ToListAsync();

        var notifications = new List<Notification>();

        foreach (var userId in watchers)
        {
            if (userId == excludeUserId) continue;

            notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                Link = link,
                CreatedOn = DateTime.UtcNow,
                IsRead = false
            });
        }

        if (notifications.Any())
        {
            context.Notifications.AddRange(notifications);
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        var notification = await context.Notifications.FindAsync(notificationId);
        if (notification != null && notification.UserId == userId)
        {
            notification.IsRead = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        var unread = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        return await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();

        return await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Message = n.Message,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedOn = n.CreatedOn
            })
            .ToListAsync();
    }
}
