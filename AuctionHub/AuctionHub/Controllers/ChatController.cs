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

    public async Task<IActionResult> Index(int? auctionId = null, string? targetUserId = null, bool global = false)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Challenge();

        var sessions = await _chatService.GetUserChatSessionsAsync(currentUserId);
        ViewBag.Sessions = sessions;

        // Determine active chat
        if (global || (auctionId == null && targetUserId == null && !sessions.Any()))
        {
            ViewBag.ActiveChatType = "Global";
            var messages = await _chatService.GetGlobalMessagesAsync();
            return View(messages);
        }

        if (auctionId.HasValue)
        {
            // Trying to access a private chat
            bool canAccess = await _chatService.CanAccessPrivateChatAsync(auctionId.Value, currentUserId);
            if (!canAccess)
            {
                TempData["Error"] = "You do not have permission to access this chat.";
                return RedirectToAction(nameof(Index), new { global = true });
            }

            var auction = await _auctionService.GetAuctionDetailsAsync(auctionId.Value, currentUserId);
            if (auction == null) return NotFound();

            string otherUserId;
            if (!string.IsNullOrEmpty(targetUserId))
            {
                otherUserId = targetUserId;
            }
            else
            {
                otherUserId = currentUserId == auction.SellerId ? (auction.WinnerId ?? "") : auction.SellerId;
            }

            if (string.IsNullOrEmpty(otherUserId))
            {
                TempData["Error"] = "The other party is not available for chat yet.";
                return RedirectToAction(nameof(Index), new { global = true });
            }

            var messages = await _chatService.GetPrivateMessagesAsync(auctionId.Value, currentUserId, otherUserId);
            var otherUser = await _userManager.FindByIdAsync(otherUserId);
            
            ViewBag.ActiveChatType = "Private";
            ViewBag.AuctionId = auctionId.Value;
            ViewBag.AuctionTitle = auction.Title;
            ViewBag.OtherUserId = otherUserId;
            ViewBag.OtherUserName = otherUser?.DisplayName ?? otherUser?.UserName ?? "User";
            ViewBag.CurrentUserId = currentUserId;

            return View(messages);
        }

        // If no specific chat selected but sessions exist, pick the most recent one
        if (sessions.Any())
        {
            var latest = sessions.First();
            if (latest.IsGlobal)
            {
                return RedirectToAction(nameof(Index), new { global = true });
            }
            else
            {
                return RedirectToAction(nameof(Index), new { auctionId = latest.AuctionId, targetUserId = latest.OtherUserId });
            }
        }

        return View(new List<AuctionHub.Application.DTOs.ChatMessageDto>());
    }
}