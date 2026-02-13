using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface INotificationService
{
    Task NotifyUserAsync(string userId, string message, string? link = null);
    Task NotifyAllUsersAsync(string message, string? link = null);
    Task NotifyAllWatchersAsync(int auctionId, string message, string? link = null, string? excludeUserId = null);
    Task MarkAsReadAsync(int notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId);
}
