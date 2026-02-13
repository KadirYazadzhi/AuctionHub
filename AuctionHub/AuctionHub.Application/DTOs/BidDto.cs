namespace AuctionHub.Application.DTOs;

public class BidDto
{
    public decimal Amount { get; set; }
    public DateTime BidTime { get; set; }
    public string Bidder { get; set; } = null!;
    public string AuctionTitle { get; set; } = null!;
}
