namespace AuctionHub.Application.DTOs;

public class SellerAnalyticsDto
{
    public int TotalActiveAuctions { get; set; }
    public int TotalViews { get; set; }
    public int TotalWatchlistAdds { get; set; }
    public decimal TotalRevenue { get; set; }
    
    // For the Chart: Views per auction
    public List<string> AuctionTitles { get; set; } = new();
    public List<int> AuctionViews { get; set; } = new();
    public List<int> AuctionWatchlistCounts { get; set; } = new();
}
