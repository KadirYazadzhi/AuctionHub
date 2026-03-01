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

            // Run once every 1 hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
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
            // Optimization: Filter by IsSettled and IsDisputed
            var expiredAuctions = await context.Auctions
                .Include(a => a.Seller)
                .Include(a => a.Bids)
                .Where(a => !a.IsActive && 
                            a.EndTime <= sevenDaysAgo && 
                            !a.IsSettled && 
                            !a.IsDisputed)
                .ToListAsync();

            var commissionRate = await GetCommissionRateAsync(context);

            foreach (var auction in expiredAuctions)
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try 
                {
                    var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
                    if (winningBid == null) 
                    {
                        auction.IsSettled = true;
                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        continue;
                    }

                    _logger.LogInformation($"Auto-releasing escrow for auction {auction.Id}: {auction.Title}");

                    // 1. Calculate Commission
                    decimal totalAmount = winningBid.Amount;
                    decimal commissionAmount = Math.Round(totalAmount * commissionRate, 2);
                    decimal finalSellerAmount = totalAmount - commissionAmount;

                    // 2. Release Funds to Seller
                    auction.Seller.WalletBalance += finalSellerAmount;
                    auction.IsSettled = true;

                    context.Transactions.Add(new Transaction
                    {
                        UserId = auction.SellerId,
                        Amount = finalSellerAmount,
                        Description = $"Sale of '{auction.Title}' - Auto-released after 7 days (Gross: {totalAmount:C}, Commission: {commissionAmount:C})",
                        TransactionType = "Sale",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auction.Id
                    });

                    // 3. System Commission Log
                    var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@auctionhub.com");
                    if (adminUser != null)
                    {
                        context.Transactions.Add(new Transaction
                        {
                            UserId = adminUser.Id,
                            Amount = commissionAmount,
                            Description = $"Commission from auction '{auction.Title}' (Auto-released)",
                            TransactionType = "Commission",
                            TransactionDate = DateTime.UtcNow,
                            AuctionId = auction.Id
                        });
                    }

                    // 4. Notify Seller
                    await notificationService.NotifyUserAsync(auction.SellerId, 
                        $"💰 Payment for '{auction.Title}' auto-released: {finalSellerAmount:C}. The 7-day confirmation period has passed.", 
                        $"/Auctions/Details/{auction.Id}");

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error releasing escrow for auction {auction.Id}");
                }
            }
        }
    }

    private async Task<decimal> GetCommissionRateAsync(IAuctionHubDbContext context)
    {
        var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CommissionRate");
        if (setting != null && decimal.TryParse(setting.Value, out decimal rate))
        {
            return rate / 100m;
        }
        return 0.05m;
    }
}
