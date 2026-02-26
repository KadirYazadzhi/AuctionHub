namespace AuctionHub.Application.DTOs;

public class ReviewDto
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public string ReviewerName { get; set; } = null!;
    public string ReviewerId { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
    public string AuctionTitle { get; set; } = null!;
}
