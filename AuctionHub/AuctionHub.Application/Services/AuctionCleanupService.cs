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

            await ProcessDutchAuctionsAsync();
            await CloseExpiredAuctionsAsync();

            // Run every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessDutchAuctionsAsync()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IAuctionHubDbContext>();
            var biddingNotifier = scope.ServiceProvider.GetRequiredService<IBiddingNotificationService>();
            
            var now = DateTime.UtcNow;
            
            // Find active Dutch auctions that are due for a price drop
            var dutchAuctions = await context.Auctions
                .Where(a => a.IsActive && a.IsDutchAuction && a.LastDutchDecrement.HasValue && a.DutchDecrementIntervalMinutes.HasValue && a.DutchDecrementAmount.HasValue)
                .ToListAsync();

            var auctionsToUpdate = dutchAuctions
                .Where(a => a.LastDutchDecrement.Value.AddMinutes(a.DutchDecrementIntervalMinutes.Value) <= now)
                .ToList();

            foreach (var auction in auctionsToUpdate)
            {
                // Calculate minimum allowed price (ReservePrice or 0)
                decimal minPrice = auction.ReservePrice ?? 0.01m;
                
                if (auction.CurrentPrice > minPrice)
                {
                    auction.CurrentPrice -= auction.DutchDecrementAmount.Value;
                    if (auction.CurrentPrice < minPrice)
                    {
                        auction.CurrentPrice = minPrice;
                    }
                    
                    auction.LastDutchDecrement = now;
                    
                    // Notify clients in real-time
                    await biddingNotifier.NotifyNewBidAsync(auction.Id, "System (Price Drop)", auction.CurrentPrice, now);
                }
            }

            if (auctionsToUpdate.Any())
            {
                await context.SaveChangesAsync();
            }
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
                foreach (var auction in expiredAuctions)
                {
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        auction.IsActive = false;
                        _logger.LogInformation($"Closing auction {auction.Id}: {auction.Title}");

                        var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();

                        if (winningBid != null)
                        {
                            bool reserveMet = !auction.ReservePrice.HasValue || winningBid.Amount >= auction.ReservePrice.Value;

                            if (reserveMet)
                            {
                                // 1. Log Escrow (Funds are held)
                                context.Transactions.Add(new Transaction
                                {
                                    UserId = auction.SellerId,
                                    Amount = winningBid.Amount,
                                    Description = $"Escrow: Payment for '{auction.Title}' held until delivery confirmation (Auction ID: {auction.Id}).",
                                    TransactionType = "Escrow",
                                    TransactionDate = DateTime.UtcNow,
                                    AuctionId = auction.Id
                                });

                                // 2. Notify Winner
                                await notificationService.NotifyUserAsync(winningBid.BidderId, 
                                    $"🎉 Congratulations! You won the auction for '{auction.Title}' with a bid of {winningBid.Amount:C}! Please confirm receipt in the auction details to release funds to the seller.", 
                                    $"/Auctions/Details/{auction.Id}");

                                // 3. Notify Seller
                                await notificationService.NotifyUserAsync(auction.SellerId, 
                                    $"💰 Your item '{auction.Title}' was sold to {winningBid.Bidder.DisplayName ?? winningBid.Bidder.UserName} for {winningBid.Amount:C}!", 
                                    $"/Auctions/Details/{auction.Id}");

                                // Notify Losers
                                var losingBidders = auction.Bids
                                    .Where(b => b.BidderId != winningBid.BidderId)
                                    .Select(b => b.BidderId)
                                    .Distinct()
                                    .ToList();

                                foreach (var loserId in losingBidders)
                                {
                                    try 
                                    {
                                        await notificationService.NotifyUserAsync(loserId, 
                                            $"🔔 The auction for '{auction.Title}' has ended. Unfortunately, you did not win this time.", 
                                            $"/Auctions/Details/{auction.Id}");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"Failed to notify loser {loserId}: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                // Reserve not met
                                // Refund the highest bidder
                                var highestBidder = winningBid.Bidder;
                                highestBidder.WalletBalance += winningBid.Amount;
                                
                                context.Transactions.Add(new Transaction
                                {
                                    UserId = highestBidder.Id,
                                    Amount = winningBid.Amount,
                                    Description = $"Refund: Reserve price not met for '{auction.Title}'",
                                    TransactionType = "Refund",
                                    TransactionDate = DateTime.UtcNow,
                                    AuctionId = auction.Id
                                });

                                await notificationService.NotifyUserAsync(winningBid.BidderId, 
                                    $"🔔 The auction for '{auction.Title}' ended, but your bid of {winningBid.Amount:C} did not meet the reserve price. Your funds have been refunded.", 
                                    $"/Auctions/Details/{auction.Id}");

                                await notificationService.NotifyUserAsync(auction.SellerId, 
                                    $"📉 Your auction for '{auction.Title}' has ended, but the highest bid ({winningBid.Amount:C}) did not meet your reserve price of {auction.ReservePrice:C}.", 
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

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, $"Error occurred while closing auction {auction.Id}.");
                    }
                }
            }
        }
    }
}