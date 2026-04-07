using System.Threading.Tasks;
using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IStatsNotificationService
{
    Task UpdateHomeStatsAsync(HomeStatsDto stats);
}
