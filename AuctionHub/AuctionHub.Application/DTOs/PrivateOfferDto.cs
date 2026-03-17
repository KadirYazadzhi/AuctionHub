namespace AuctionHub.Application.DTOs;

public class PrivateOfferDto
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public string BuyerId { get; set; } = null!;
    public string BuyerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
}
