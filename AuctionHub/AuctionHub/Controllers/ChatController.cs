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
            else
            {
                // If auction not found by PublicId, log this and maybe fallback to global
                TempData["Error"] = "The auction associated with this chat could not be found.";
                return RedirectToAction(nameof(Index), new { global = true });
            }
        }

        var sessions = await _chatService.GetUserChatSessionsAsync(currentUserId);
        var sessionsList = sessions.ToList();

        // Determine active chat
        if (global || (auctionId == null && targetUserId == null && !sessionsList.Any()))
        {
            ViewBag.Sessions = sessionsList;
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
                string? referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer)) return Redirect(referer);
                return RedirectToAction(nameof(Index), new { global = true });
            }

            var auction = await _auctionService.GetAuctionDetailsAsync(auctionId.Value, currentUserId);
            if (auction == null) return NotFound();

            string otherUserId = "";
            if (!string.IsNullOrEmpty(targetUserId))
            {
                otherUserId = targetUserId;
            }
            else
            {
                // Try to find the last person this user talked to regarding this auction
                var lastMessage = await _chatService.GetLastMessageForSessionAsync(auctionId.Value, currentUserId);
                if (lastMessage != null)
                {
                    otherUserId = lastMessage.SenderId == currentUserId ? (lastMessage.ReceiverId ?? "") : lastMessage.SenderId;
                }
                
                // Fallback to traditional seller/winner logic if no messages yet
                if (string.IsNullOrEmpty(otherUserId))
                {
                    otherUserId = currentUserId == auction.SellerId ? (auction.WinnerId ?? "") : auction.SellerId;
                }
            }

            if (string.IsNullOrEmpty(otherUserId))
            {
                TempData["Error"] = "The other party is not available for chat yet.";
                string? referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer)) return Redirect(referer);
                return RedirectToAction(nameof(Index), new { global = true });
            }

            // Ensure this session is in the sidebar even if it has no messages
            if (!sessionsList.Any(s => s.AuctionId == auctionId && s.OtherUserId == otherUserId))
            {
                var otherUserObj = await _userManager.FindByIdAsync(otherUserId);
                sessionsList.Add(new AuctionHub.Application.DTOs.ChatSessionDto
                {
                    AuctionId = auctionId,
                    AuctionTitle = auction.Title,
                    OtherUserId = otherUserId,
                    OtherUserName = otherUserObj?.DisplayName ?? "User",
                    OtherUserAvatar = otherUserObj?.ProfilePictureUrl,
                    LastMessage = "Starting conversation...",
                    LastMessageTime = DateTime.UtcNow
                });
            }

            ViewBag.Sessions = sessionsList.OrderByDescending(s => s.LastMessageTime).ToList();

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

        ViewBag.Sessions = sessionsList;

        // If no specific chat selected but sessions exist, pick the most recent one
        if (sessions.Any())
        {
            var privateSessions = sessions.Where(s => !s.IsGlobal).ToList();
            if (privateSessions.Any())
            {
                var latest = privateSessions.First();
                // Safety check: only redirect if access is actually granted
                bool canAccess = await _chatService.CanAccessPrivateChatAsync(latest.AuctionId!.Value, currentUserId);
                if (canAccess)
                {
                    return RedirectToAction(nameof(Index), new { auctionId = latest.AuctionId, targetUserId = latest.OtherUserId });
                }
            }
            
            // Default to global if no accessible private sessions
            return RedirectToAction(nameof(Index), new { global = true });
        }

        return View(new List<AuctionHub.Application.DTOs.ChatMessageDto>());
    }
}