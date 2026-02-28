using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuctionHub.Application.Services;

public class EscrowReleaseService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscrowReleaseService> _logger;

    public EscrowReleaseService(IServiceProvider serviceProvider, ILogger<EscrowReleaseService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Escrow Release Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredEscrowsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while releasing expired escrows.");
            }

            // Run once every 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ReleaseExpiredEscrowsAsync()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            // Find auctions that ended more than 7 days ago and are still in Escrow
            // (We identify Escrow by checking if a 'Sale' transaction doesn't exist yet for that auction)
            var expiredAuctions = await context.Auctions
                .Include(a => a.Seller)
                .Include(a => a.Bids)
                .Where(a => !a.IsActive && a.EndTime <= sevenDaysAgo)
                .ToListAsync();

            foreach (var auction in expiredAuctions)
            {
                var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
                if (winningBid == null) continue;

                // Check if already released (Sale transaction exists)
                var alreadyReleased = await context.Transactions
                    .AnyAsync(t => t.UserId == auction.SellerId && 
                                   t.TransactionType == "Sale" && 
                                   t.Description.Contains($"(Auction ID: {auction.Id})"));

                if (alreadyReleased) continue;

                _logger.LogInformation($"Auto-releasing escrow for auction {auction.Id}: {auction.Title}");

                // 1. Release Funds to Seller
                auction.Seller.WalletBalance += winningBid.Amount;

                context.Transactions.Add(new Transaction
                {
                    UserId = auction.SellerId,
                    Amount = winningBid.Amount,
                    Description = $"Sale of item '{auction.Title}' - Auto-released by system after 7 days (Auction ID: {auction.Id})",
                    TransactionType = "Sale",
                    TransactionDate = DateTime.UtcNow
                });

                // 2. Notify Seller
                await notificationService.NotifyUserAsync(auction.SellerId, 
                    $"💰 Payment for '{auction.Title}' has been automatically released to your wallet. The 7-day confirmation period has passed.", 
                    $"/Auctions/Details/{auction.Id}");
            }

            await context.SaveChangesAsync();
        }
    }
}
