using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class ChatService : IChatService
{
    private readonly IAuctionHubDbContext _context;

    public ChatService(IAuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ChatMessageDto>> GetGlobalMessagesAsync(int limit = 50)
    {
        return await _context.ChatMessages
            .Where(m => m.IsGlobal)
            .OrderByDescending(m => m.SentOn)
            .Take(limit)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.DisplayName ?? m.Sender.UserName ?? "Unknown",
                SenderAvatar = m.Sender.ProfilePictureUrl,
                Content = m.Content,
                SentOn = m.SentOn,
                IsGlobal = true
            })
            .OrderBy(m => m.SentOn) // Return chronological order for UI
            .ToListAsync();
    }

    public async Task<IEnumerable<ChatSessionDto>> GetUserChatSessionsAsync(string userId)
    {
        var sessions = new List<ChatSessionDto>();

        // 1. Global Chat Session
        var lastGlobalMessage = await _context.ChatMessages
            .Where(m => m.IsGlobal)
            .OrderByDescending(m => m.SentOn)
            .FirstOrDefaultAsync();

        if (lastGlobalMessage != null)
        {
            sessions.Add(new ChatSessionDto
            {
                IsGlobal = true,
                LastMessage = lastGlobalMessage.Content,
                LastMessageTime = lastGlobalMessage.SentOn,
                OtherUserName = "Global Chat"
            });
        }
        else
        {
            sessions.Add(new ChatSessionDto
            {
                IsGlobal = true,
                LastMessage = "No messages yet.",
                LastMessageTime = DateTime.MinValue,
                OtherUserName = "Global Chat"
            });
        }

        // 2. Private Chat Sessions
        var privateMessages = await _context.ChatMessages
            .Include(m => m.Auction)
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => !m.IsGlobal && (m.SenderId == userId || m.ReceiverId == userId))
            .ToListAsync();

        var groupedPrivateSessions = privateMessages
            .GroupBy(m => new 
            { 
                m.AuctionId, 
                OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId 
            })
            .Select(g => 
            {
                var lastMsg = g.OrderByDescending(m => m.SentOn).First();
                var otherUser = lastMsg.SenderId == userId ? lastMsg.Receiver : lastMsg.Sender;
                return new ChatSessionDto
                {
                    IsGlobal = false,
                    AuctionId = g.Key.AuctionId,
                    AuctionTitle = lastMsg.Auction?.Title ?? "Unknown Auction",
                    OtherUserId = g.Key.OtherUserId,
                    OtherUserName = otherUser?.DisplayName ?? otherUser?.UserName ?? "Unknown User",
                    OtherUserAvatar = otherUser?.ProfilePictureUrl,
                    LastMessage = lastMsg.Content,
                    LastMessageTime = lastMsg.SentOn
                };
            });

        sessions.AddRange(groupedPrivateSessions);

        return sessions.OrderByDescending(s => s.LastMessageTime).ToList();
    }

    public async Task<IEnumerable<ChatMessageDto>> GetPrivateMessagesAsync(int auctionId, string userId1, string userId2)
    {
        return await _context.ChatMessages
            .Where(m => m.AuctionId == auctionId && !m.IsGlobal &&
                        ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                         (m.SenderId == userId2 && m.ReceiverId == userId1)))
            .OrderByDescending(m => m.SentOn)
            .Take(50)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.DisplayName ?? m.Sender.UserName ?? "Unknown",
                SenderAvatar = m.Sender.ProfilePictureUrl,
                ReceiverId = m.ReceiverId,
                ReceiverName = m.Receiver!.DisplayName ?? m.Receiver.UserName ?? "Unknown",
                AuctionId = m.AuctionId,
                Content = m.Content,
                SentOn = m.SentOn,
                IsGlobal = false
            })
            .OrderBy(m => m.SentOn)
            .ToListAsync();
    }

    public async Task<ChatMessageDto> SaveMessageAsync(string senderId, string content, bool isGlobal, string? receiverId = null, int? auctionId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.");
        }

        if (content.Length > 1000)
        {
            content = content.Substring(0, 1000); // Truncate to prevent DB overflow
        }

        var msg = new ChatMessage
        {
            SenderId = senderId,
            Content = content.Trim(),
            IsGlobal = isGlobal,
            ReceiverId = receiverId,
            AuctionId = auctionId,
            SentOn = DateTime.UtcNow
        };

        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        // Fetch sender/receiver details for the DTO
        var sender = await _context.Users.FindAsync(senderId);
        ApplicationUser? receiver = null;
        if (receiverId != null)
        {
            receiver = await _context.Users.FindAsync(receiverId);
        }

        return new ChatMessageDto
        {
            Id = msg.Id,
            SenderId = msg.SenderId,
            SenderName = sender?.DisplayName ?? sender?.UserName ?? "Unknown",
            SenderAvatar = sender?.ProfilePictureUrl,
            ReceiverId = msg.ReceiverId,
            ReceiverName = receiver?.DisplayName ?? receiver?.UserName,
            AuctionId = msg.AuctionId,
            Content = msg.Content,
            SentOn = msg.SentOn,
            IsGlobal = msg.IsGlobal
        };
    }

    public async Task<bool> CanAccessPrivateChatAsync(int auctionId, string userId)
    {
        // 1. Check if user is Administrator (Admins have "Master Key" access)
        var isAdmin = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .AnyAsync(x => x.ur.UserId == userId && x.r.Name == "Administrator");

        if (isAdmin) return true;

        var auction = await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null) return false;
        
        // Chat is only available if auction is closed
        if (auction.IsActive) return false;

        // Is User the Seller?
        if (auction.SellerId == userId) return true;

        // Is User the Winner (Highest Bidder)?
        var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (highestBid != null && highestBid.BidderId == userId) return true;

        return false;
    }
}
