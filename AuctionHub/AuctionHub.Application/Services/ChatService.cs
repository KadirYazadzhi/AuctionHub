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
            .Include(m => m.Sender)
            .Where(m => m.IsGlobal)
            .OrderByDescending(m => m.SentOn)
            .Take(limit)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender != null ? (m.Sender.DisplayName ?? m.Sender.UserName ?? "Unknown") : "Unknown",
                SenderAvatar = m.Sender != null ? m.Sender.ProfilePictureUrl : null,
                Content = m.Content,
                SentOn = m.SentOn,
                IsGlobal = true
            })
            .OrderBy(m => m.SentOn)
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

        sessions.Add(new ChatSessionDto
        {
            IsGlobal = true,
            LastMessage = lastGlobalMessage?.Content ?? "No messages yet.",
            LastMessageTime = lastGlobalMessage?.SentOn ?? DateTime.MinValue,
            OtherUserName = "Global Chat"
        });

        // 2. Private Chat Sessions
        var privateMessages = await _context.ChatMessages
            .Include(m => m.Auction)
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => !m.IsGlobal && (m.SenderId == userId || m.ReceiverId == userId))
            .Where(m => (m.SenderId == userId && !m.IsHiddenForSender) || (m.ReceiverId == userId && !m.IsHiddenForReceiver))
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
                    AuctionTitle = lastMsg.Auction != null ? (lastMsg.Auction.Title ?? "Unknown Auction") : "Unknown Auction",
                    OtherUserId = g.Key.OtherUserId,
                    OtherUserPublicId = otherUser?.PublicId ?? Guid.Empty,
                    OtherUserName = otherUser != null ? (otherUser.DisplayName ?? otherUser.UserName ?? "Unknown User") : "Unknown User",
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
        var isAdmin1 = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .AnyAsync(x => x.ur.UserId == userId1 && x.r.Name == "Administrator");

        var query = _context.ChatMessages
            .Where(m => m.AuctionId == auctionId && !m.IsGlobal &&
                        ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                         (m.SenderId == userId2 && m.ReceiverId == userId1)));

        // Admins can see everything, others only what's not hidden for them
        if (!isAdmin1)
        {
            query = query.Where(m => (m.SenderId == userId1 && !m.IsHiddenForSender) || (m.ReceiverId == userId1 && !m.IsHiddenForReceiver));
        }

        return await query
            .OrderByDescending(m => m.SentOn)
            .Take(50)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.DisplayName ?? m.Sender.UserName ?? "Unknown",
                SenderAvatar = m.Sender.ProfilePictureUrl,
                ReceiverId = m.ReceiverId,
                ReceiverName = m.Receiver != null ? (m.Receiver.DisplayName ?? m.Receiver.UserName ?? "Unknown") : "Unknown",
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
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Message content cannot be empty.");
        if (content.Length > 1000) content = content.Substring(0, 1000);

        // NEW: If this is a private message, unhide the ENTIRE previous conversation for BOTH participants
        // Use a more robust case-insensitive check in the unhiding logic
        if (!isGlobal && auctionId.HasValue && !string.IsNullOrEmpty(receiverId))
        {
            var previousMessages = await _context.ChatMessages
                .Where(m => m.AuctionId == auctionId && 
                           ((m.SenderId == senderId && m.ReceiverId == receiverId) || 
                            (m.SenderId == receiverId && m.ReceiverId == senderId)))
                .ToListAsync();

            foreach (var prevMsg in previousMessages)
            {
                // Unhide for current sender
                if (string.Equals(prevMsg.SenderId, senderId, StringComparison.OrdinalIgnoreCase)) prevMsg.IsHiddenForSender = false;
                if (string.Equals(prevMsg.ReceiverId, senderId, StringComparison.OrdinalIgnoreCase)) prevMsg.IsHiddenForReceiver = false;
                
                // Unhide for current receiver
                if (string.Equals(prevMsg.SenderId, receiverId, StringComparison.OrdinalIgnoreCase)) prevMsg.IsHiddenForSender = false;
                if (string.Equals(prevMsg.ReceiverId, receiverId, StringComparison.OrdinalIgnoreCase)) prevMsg.IsHiddenForReceiver = false;
            }
        }

        var msg = new ChatMessage
        {
            SenderId = senderId,
            Content = content.Trim(),
            IsGlobal = isGlobal,
            ReceiverId = receiverId,
            AuctionId = auctionId,
            SentOn = DateTime.UtcNow,
            IsHiddenForSender = false,
            IsHiddenForReceiver = false
        };

        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(senderId);
        var receiver = receiverId != null ? await _context.Users.FindAsync(receiverId) : null;

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
        var isAdmin = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .AnyAsync(x => x.ur.UserId == userId && x.r.Name == "Administrator");

        if (isAdmin) return true;

        var hasConversation = await _context.ChatMessages
            .AnyAsync(m => m.AuctionId == auctionId && (m.SenderId == userId || m.ReceiverId == userId));
        
        if (hasConversation) return true;

        var auction = await _context.Auctions.Include(a => a.Bids).FirstOrDefaultAsync(a => a.Id == auctionId);
        if (auction == null) return false;
        
        if (auction.SellerId == userId) return true;
        if (!auction.IsActive)
        {
            var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (highestBid != null && highestBid.BidderId == userId) return true;
        }

        return false;
    }

    public async Task<AuctionDto?> GetAuctionByIdAsync(int auctionId)
    {
        var auction = await _context.Auctions.Include(a => a.Category).FirstOrDefaultAsync(a => a.Id == auctionId);
        if (auction == null) return null;

        return new AuctionDto
        {
            Id = auction.Id,
            PublicId = auction.PublicId,
            Title = auction.Title,
            ImageUrl = auction.ImageUrl,
            CurrentPrice = auction.CurrentPrice,
            EndTime = auction.EndTime,
            Category = auction.Category?.Name ?? "General",
            IsActive = auction.IsActive
        };
    }

    public async Task<ChatMessageDto?> GetLastMessageForSessionAsync(int auctionId, string userId)
    {
        var msg = await _context.ChatMessages
            .Where(m => m.AuctionId == auctionId && (m.SenderId == userId || m.ReceiverId == userId))
            .OrderByDescending(m => m.SentOn)
            .FirstOrDefaultAsync();

        if (msg == null) return null;
        return new ChatMessageDto { Id = msg.Id, SenderId = msg.SenderId, ReceiverId = msg.ReceiverId, AuctionId = msg.AuctionId, Content = msg.Content, SentOn = msg.SentOn };
    }

    public async Task<bool> DeleteChatAsync(int auctionId, string userId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.AuctionId == auctionId && (m.SenderId == userId || m.ReceiverId == userId))
            .ToListAsync();

        if (!messages.Any()) return false;

        foreach (var m in messages)
        {
            if (string.Equals(m.SenderId, userId, StringComparison.OrdinalIgnoreCase)) m.IsHiddenForSender = true;
            if (string.Equals(m.ReceiverId, userId, StringComparison.OrdinalIgnoreCase)) m.IsHiddenForReceiver = true;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
