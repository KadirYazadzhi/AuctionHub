using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AuctionHub.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
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
    }
}
