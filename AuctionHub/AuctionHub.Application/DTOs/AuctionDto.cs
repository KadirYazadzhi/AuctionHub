namespace AuctionHub.Application.DTOs;

public class AuctionDto
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Title { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = null!;
    public int CategoryId { get; set; }
    public bool IsActive { get; set; }
    public bool IsPromoted { get; set; }
    public bool IsSuspended { get; set; }
    public string SellerId { get; set; } = null!;
    public string SellerName { get; set; } = null!;
    public bool IsTopSeller { get; set; }
    public bool? IsWinning { get; set; }
    public string? WinnerId { get; set; }
}
