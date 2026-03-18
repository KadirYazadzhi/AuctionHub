using AuctionHub.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuctionHub.Infrastructure.Services;

public class AuctionBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionBackgroundWorker> _logger;

    public AuctionBackgroundWorker(IServiceProvider serviceProvider, ILogger<AuctionBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auction Background Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var auctionService = scope.ServiceProvider.GetRequiredService<IAuctionService>();
                    
                    _logger.LogInformation("Background Worker: Running Escrow Release and Dutch Auction processing.");
                    
                    await auctionService.ReleaseEscrowFundsAsync();
                    await auctionService.ProcessDutchAuctionsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Auction Background Worker.");
            }

            // Run every 10 minutes (configurable or as needed)
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }

        _logger.LogInformation("Auction Background Worker is stopping.");
    }
}
