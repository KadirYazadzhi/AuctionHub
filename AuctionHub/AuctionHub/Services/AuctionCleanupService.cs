using AuctionHub.Data;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Services;

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
            var context = scope.ServiceProvider.GetRequiredService<AuctionHubDbContext>();
            
            // Find active auctions that have passed their EndTime
            var expiredAuctions = await context.Auctions
                .Where(a => a.IsActive && a.EndTime <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredAuctions.Any())
            {
                foreach (var auction in expiredAuctions)
                {
                    auction.IsActive = false;
                    _logger.LogInformation($"Closing auction {auction.Id}: {auction.Title}");
                    
                    // Here we could add logic to notify the winner or seller via email/notification DB table
                }

                await context.SaveChangesAsync();
            }
        }
    }
}
