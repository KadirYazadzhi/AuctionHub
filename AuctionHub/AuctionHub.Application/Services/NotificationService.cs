using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IAuctionHubDbContext _context;

    public NotificationService(IAuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task NotifyUserAsync(string userId, string message, string? link = null)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            Link = link,
            CreatedOn = DateTime.UtcNow,
            IsRead = false
        });

        // If we are NOT in a transaction, save immediately. 
        // If we ARE in a transaction, this does nothing because we will save later in the service.
        // Actually, for consistency, we should only SaveChanges here if we want immediate effect.
        // In the context of PlaceBid, this will participate in the transaction.
        await _context.SaveChangesAsync();
    }

    public async Task NotifyAllUsersAsync(string message, string? link = null)
    {
        var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
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

        await _context.Notifications.AddRangeAsync(notifications);
        await _context.SaveChangesAsync();
    }

    public async Task NotifyAllWatchersAsync(int auctionId, string message, string? link = null, string? excludeUserId = null)
    {
        var watchers = await _context.Watchlist
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
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && notification.UserId == userId)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId)
    {
        return await _context.Notifications
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
