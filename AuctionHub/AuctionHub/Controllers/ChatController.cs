using System.Security.Claims;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly IAuctionService _auctionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatController(IChatService chatService, IAuctionService auctionService, UserManager<ApplicationUser> userManager)
    {
        _chatService = chatService;
        _auctionService = auctionService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? auctionId = null, string? targetUserId = null, bool global = false, Guid? publicId = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        // If we got a publicId (modern link), convert it to internal id
        if (publicId.HasValue && publicId != Guid.Empty)
        {
            var auctionDto = await _auctionService.GetAuctionDetailsAsync(publicId.Value, currentUserId);
            if (auctionDto != null)
            {
                auctionId = auctionDto.Id;
            }
        }

        var sessions = await _chatService.GetUserChatSessionsAsync(currentUserId);
        var sessionsList = sessions.ToList();

        // Determine active chat - DEFAULT to Global if no specific private chat is requested
        if (global || (auctionId == null && targetUserId == null))
        {
            ViewBag.Sessions = sessionsList;
            ViewBag.ActiveChatType = "Global";
            var messages = await _chatService.GetGlobalMessagesAsync();
            return View(messages);
        }

        if (auctionId.HasValue)
        {
            // ... (rest of the private chat logic stays the same)
            // Trying to access a private chat
            bool canAccess = await _chatService.CanAccessPrivateChatAsync(auctionId.Value, currentUserId);
            if (!canAccess)
            {
                TempData["Error"] = "You do not have permission to access this chat.";
                return RedirectToAction(nameof(Index), new { global = true });
            }

            var auction = await _auctionService.GetAuctionDetailsAsync(auctionId.Value, currentUserId);
            if (auction == null)
            {
                ViewBag.IsArchived = true;
                ViewBag.AuctionTitle = "Archived Auction";
            }
            else
            {
                ViewBag.IsArchived = !auction.IsActive;
                ViewBag.AuctionTitle = auction.Title;
            }

            string otherUserId = "";
            if (!string.IsNullOrEmpty(targetUserId))
            {
                otherUserId = targetUserId;
            }
            else
            {
                // If targetUserId not provided, look at history
                var lastMessage = await _chatService.GetLastMessageForSessionAsync(auctionId.Value, currentUserId);
                if (lastMessage != null)
                {
                    otherUserId = lastMessage.SenderId == currentUserId ? (lastMessage.ReceiverId ?? "") : lastMessage.SenderId;
                }
                
                // Fallback to Seller or Winner if no history
                if (string.IsNullOrEmpty(otherUserId) && auction != null)
                {
                    otherUserId = currentUserId == auction.SellerId ? (auction.WinnerId ?? "") : auction.SellerId;
                }
            }

            if (string.IsNullOrEmpty(otherUserId))
            {
                TempData["Error"] = "The other party is not available for chat yet.";
                return RedirectToAction(nameof(Index), new { global = true });
            }

            // Ensure this session is in the sidebar list (important for SignalR updates)
            if (!sessionsList.Any(s => s.AuctionId == auctionId && string.Equals(s.OtherUserId, otherUserId, StringComparison.OrdinalIgnoreCase)))
            {
                var otherUserObj = await _userManager.FindByIdAsync(otherUserId);
                var lastMsgForSidebar = await _chatService.GetLastMessageForSessionAsync(auctionId.Value, currentUserId);
                
                sessionsList.Add(new AuctionHub.Application.DTOs.ChatSessionDto
                {
                    IsGlobal = false,
                    AuctionId = auctionId,
                    AuctionTitle = auction?.Title ?? "Archived Auction",
                    OtherUserId = otherUserId,
                    OtherUserName = otherUserObj?.DisplayName ?? otherUserObj?.UserName ?? "User",
                    OtherUserAvatar = otherUserObj?.ProfilePictureUrl,
                    LastMessage = lastMsgForSidebar?.Content ?? "No messages yet",
                    LastMessageTime = lastMsgForSidebar?.SentOn ?? DateTime.UtcNow
                });
            }

            ViewBag.Sessions = sessionsList.OrderByDescending(s => s.LastMessageTime).ToList();

            var messages = await _chatService.GetPrivateMessagesAsync(auctionId.Value, currentUserId, otherUserId);
            var otherUser = await _userManager.FindByIdAsync(otherUserId);
            
            ViewBag.ActiveChatType = "Private";
            ViewBag.AuctionId = auctionId.Value;
            ViewBag.OtherUserId = otherUserId;
            ViewBag.OtherUserName = otherUser?.DisplayName ?? otherUser?.UserName ?? "User";
            ViewBag.CurrentUserId = currentUserId;

            return View(messages);
        }

        ViewBag.Sessions = sessionsList;

        // If we reach here and no specific chat is active, just return an empty global-like state or redirect to global
        // Since we already handled the default Global at the beginning, this part can just return the global view if all else fails
        ViewBag.ActiveChatType = "Global";
        var fallbackMessages = await _chatService.GetGlobalMessagesAsync();
        return View(fallbackMessages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChat(int auctionId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var success = await _chatService.DeleteChatAsync(auctionId, currentUserId);
        if (success)
        {
            TempData["Success"] = "Conversation deleted successfully.";
        }
        else
        {
            TempData["Error"] = "Could not delete conversation.";
        }

        return RedirectToAction(nameof(Index), new { global = true });
    }
}
