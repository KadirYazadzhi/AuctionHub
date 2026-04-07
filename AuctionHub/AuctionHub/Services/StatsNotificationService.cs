using System.Threading.Tasks;
using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AuctionHub.Services
{
    public class StatsNotificationService : IStatsNotificationService
    {
        private readonly IHubContext<StatsHub> _hubContext;

        public StatsNotificationService(IHubContext<StatsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task UpdateHomeStatsAsync(HomeStatsDto stats)
        {
            await _hubContext.Clients.All.SendAsync("UpdateHomeStats", stats);
        }
    }
}
