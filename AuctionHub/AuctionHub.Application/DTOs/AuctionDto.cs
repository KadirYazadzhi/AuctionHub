namespace AuctionHub.Application.DTOs;

public class AuctionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = null!;
    public int CategoryId { get; set; }
    public bool IsActive { get; set; }
    public bool IsSuspended { get; set; }
    public bool? IsWinning { get; set; }
}
