namespace AuctionHub.Application.DTOs;

public class AuctionDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal StartPrice { get; set; }
    public decimal MinIncrease { get; set; }
    public decimal? BuyItNowPrice { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = null!;
    public int CategoryId { get; set; }
    public List<string> AdditionalImages { get; set; } = new();
    public string Seller { get; set; } = null!;
    public string SellerId { get; set; } = null!;
    public double SellerRating { get; set; }
    public int SellerReviewCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsDelivered { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsWatched { get; set; }
    public bool IsWinning { get; set; }
    public decimal? CurrentAutoBidLimit { get; set; }
    public string? WinnerId { get; set; }
    public List<BidDto> Bids { get; set; } = new();
}
