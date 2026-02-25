using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuctionHub.Application.Services;

public class AuctionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionCleanupService> _logger;

    public AuctionCleanupService(IServiceProvider serviceProvider, ILogger<AuctionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Auction Cleanup Service running at: {time}", DateTimeOffset.Now);

            await CloseExpiredAuctionsAsync();

            // Run every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CloseExpiredAuctionsAsync()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            
            // Find active auctions that have passed their EndTime
            var expiredAuctions = await context.Auctions
                .Include(a => a.Seller)
                .Include(a => a.Bids)
                    .ThenInclude(b => b.Bidder)
                .Where(a => a.IsActive && a.EndTime <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredAuctions.Any())
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var auction in expiredAuctions)
                    {
                        auction.IsActive = false;
                        _logger.LogInformation($"Closing auction {auction.Id}: {auction.Title}");

                        var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();

                        if (winningBid != null)
                        {
                            // 1. Credit Seller
                            auction.Seller.WalletBalance += winningBid.Amount;
                            context.Transactions.Add(new Transaction
                            {
                                UserId = auction.SellerId,
                                Amount = winningBid.Amount,
                                Description = $"Sale of item '{auction.Title}' (Auction Winner: {winningBid.Bidder.DisplayName})",
                                TransactionType = "Sale",
                                TransactionDate = DateTime.UtcNow
                            });

                            // 2. Notify Winner
                            await notificationService.NotifyUserAsync(winningBid.BidderId, 
                                $"🎉 Congratulations! You won the auction for '{auction.Title}' with a bid of {winningBid.Amount:C}! Please leave a review for the seller.", 
                                $"/Reviews/LeaveReview?auctionId={auction.Id}");

                            // 3. Notify Seller
                            await notificationService.NotifyUserAsync(auction.SellerId, 
                                $"💰 Your item '{auction.Title}' was sold to {winningBid.Bidder.DisplayName} for {winningBid.Amount:C}!", 
                                $"/Auctions/Details/{auction.Id}");

                            // Notify Losers (Everyone who bid but didn't win)
                            var losingBidders = auction.Bids
                                .Where(b => b.BidderId != winningBid.BidderId)
                                .Select(b => b.BidderId)
                                .Distinct()
                                .ToList();

                            foreach (var loserId in losingBidders)
                            {
                                await notificationService.NotifyUserAsync(loserId, 
                                    $"🔔 The auction for '{auction.Title}' has ended. Unfortunately, you did not win this time.", 
                                    $"/Auctions/Details/{auction.Id}");
                            }
                        }
                        else
                        {
                            // Notify Seller - No bids
                            await notificationService.NotifyUserAsync(auction.SellerId, 
                                $"📉 Your auction for '{auction.Title}' has ended with no bids.", 
                                $"/Auctions/Details/{auction.Id}");
                        }
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error occurred while closing expired auctions.");
                }
            }
        }
    }
}