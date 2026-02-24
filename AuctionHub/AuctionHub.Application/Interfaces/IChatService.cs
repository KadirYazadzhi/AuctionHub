using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IChatService
{
    Task<IEnumerable<ChatMessageDto>> GetGlobalMessagesAsync(int limit = 50);
    Task<IEnumerable<ChatMessageDto>> GetPrivateMessagesAsync(int auctionId, string userId1, string userId2);
    Task<ChatMessageDto> SaveMessageAsync(string senderId, string content, bool isGlobal, string? receiverId = null, int? auctionId = null);
    Task<bool> CanAccessPrivateChatAsync(int auctionId, string userId);
}
