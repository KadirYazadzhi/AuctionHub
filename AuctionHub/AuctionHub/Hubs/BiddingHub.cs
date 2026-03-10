using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace AuctionHub.Hubs;

[Authorize]
public class BiddingHub : Hub
{
    public async Task JoinAuctionGroup(int auctionId)
    {
        string groupName = $"Auction_{auctionId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveAuctionGroup(int auctionId)
    {
        string groupName = $"Auction_{auctionId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
