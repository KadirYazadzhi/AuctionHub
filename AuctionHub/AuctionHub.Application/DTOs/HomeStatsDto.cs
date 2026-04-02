namespace AuctionHub.Application.DTOs;

public class HomeStatsDto
{
    public int ActiveAuctionsCount { get; set; }
    public decimal DailyVolume { get; set; }
    public int TotalUsersCount { get; set; }
    public List<CategoryDto> FeaturedCategories { get; set; } = new();
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
}
