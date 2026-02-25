using AuctionHub.Application.Interfaces;
using AuctionHub.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace AuctionHub.Services;

public class SignalRBiddingNotificationService : IBiddingNotificationService
{
    private readonly IHubContext<BiddingHub> _hubContext;

    public SignalRBiddingNotificationService(IHubContext<BiddingHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyNewBidAsync(int auctionId, string bidderName, decimal amount, DateTime bidTime)
    {
        string groupName = $"Auction_{auctionId}";
        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewBid", amount, bidderName, bidTime.ToString("o"));
    }

    public async Task NotifyOutbidAsync(string userId, int auctionId, string auctionTitle, decimal newPrice)
    {
        // SignalR by default uses NameIdentifier (UserId) for Clients.User()
        await _hubContext.Clients.User(userId).SendAsync("ReceiveOutbidNotification", auctionId, auctionTitle, newPrice);
    }
}
