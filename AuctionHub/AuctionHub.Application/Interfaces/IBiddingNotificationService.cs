using System;
using System.Threading.Tasks;

namespace AuctionHub.Application.Interfaces;

public interface IBiddingNotificationService
{
    Task NotifyNewBidAsync(int auctionId, string bidderName, decimal amount, DateTime bidTime);
}
