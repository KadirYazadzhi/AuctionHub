using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using AuctionHub.Domain.Models;
using System.Security.Claims;

namespace AuctionHub.Hubs;

[Authorize]
public class BiddingHub : Hub
{
    private readonly UserManager<ApplicationUser> _userManager;

    public BiddingHub(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task JoinAuctionGroup(Guid auctionId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && user.IsShadowBanned)
            {
                // Silently ignore or throw error - Shadow banned users shouldn't receive live updates
                return;
            }
        }

        string groupName = $"Auction_{auctionId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveAuctionGroup(Guid auctionId)
    {
        string groupName = $"Auction_{auctionId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
