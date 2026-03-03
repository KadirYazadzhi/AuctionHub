using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AuctionHub.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatHub(IChatService chatService, INotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _chatService = chatService;
        _notificationService = notificationService;
        _userManager = userManager;
    }

    // --- Global Chat ---
    public async Task JoinGlobalChat()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "GlobalChat");
    }

    public async Task SendMessageToGlobal(string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        // Save to Database
        var savedMessage = await _chatService.SaveMessageAsync(userId, message, isGlobal: true);

        // Broadcast to everyone in the Global Chat group
        await Clients.Group("GlobalChat").SendAsync("ReceiveGlobalMessage", savedMessage);
    }
    
    public async Task LeaveGlobalChat()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "GlobalChat");
    }

    // --- Private Chat ---
    public async Task JoinPrivateChat(int auctionId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        // Validate access before allowing to join
        bool canAccess = await _chatService.CanAccessPrivateChatAsync(auctionId, userId);
        if (canAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"PrivateChat_Auction_{auctionId}");
        }
    }

    public async Task SendMessageToPrivate(int auctionId, string receiverId, string message)
    {
        var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(senderId)) return;

        // Validate access before sending
        bool canAccess = await _chatService.CanAccessPrivateChatAsync(auctionId, senderId);
        if (!canAccess) return;

        // Save to Database
        var savedMessage = await _chatService.SaveMessageAsync(senderId, message, isGlobal: false, receiverId, auctionId);

        // Send to the specific private group
        await Clients.Group($"PrivateChat_Auction_{auctionId}").SendAsync("ReceivePrivateMessage", savedMessage);

        // Notify the receiver
        var sender = await _userManager.FindByIdAsync(senderId);
        var senderName = sender?.DisplayName ?? "Someone";
        
        await _notificationService.NotifyUserAsync(receiverId, 
            $"✉️ New message from {senderName}: \"{(message.Length > 50 ? message.Substring(0, 47) + "..." : message)}\"", 
            $"/Chat/Index?auctionId={auctionId}&targetUserId={senderId}");
    }
}
