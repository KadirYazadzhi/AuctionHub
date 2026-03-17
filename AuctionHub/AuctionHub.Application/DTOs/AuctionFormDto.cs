namespace AuctionHub.Application.DTOs;

public class AuctionFormDto
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal StartPrice { get; set; }
    public decimal MinIncrease { get; set; }
    public decimal? BuyItNowPrice { get; set; }
    public decimal? ReservePrice { get; set; }

    public bool IsDutchAuction { get; set; }
    public decimal? DutchDecrementAmount { get; set; }
    public int? DutchDecrementIntervalMinutes { get; set; }
    public decimal? ParticipationFee { get; set; }
    public DateTime EndTime { get; set; }
    public int CategoryId { get; set; }
    
    // --- Location Data ---
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public List<Stream> ImageStreams { get; set; } = new();
    public List<string> ImageFileNames { get; set; } = new();
    public List<string> AdditionalImageUrls { get; set; } = new();
    public List<int> ImagesToRemoveIds { get; set; } = new();
    public bool ShouldPromote { get; set; }
}
