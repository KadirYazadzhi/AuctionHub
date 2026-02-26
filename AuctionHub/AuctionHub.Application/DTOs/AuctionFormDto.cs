namespace AuctionHub.Application.DTOs;

public class AuctionFormDto
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal StartPrice { get; set; }
    public decimal MinIncrease { get; set; }
    public decimal? BuyItNowPrice { get; set; }
    public DateTime EndTime { get; set; }
    public int CategoryId { get; set; }
    
    public List<Stream> ImageStreams { get; set; } = new();
    public List<string> ImageFileNames { get; set; } = new();
    public List<string> AdditionalImageUrls { get; set; } = new();
    public bool ShouldPromote { get; set; }
}
