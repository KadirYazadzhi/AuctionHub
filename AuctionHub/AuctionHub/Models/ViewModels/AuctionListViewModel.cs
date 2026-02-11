namespace AuctionHub.Models.ViewModels;

public class AuctionListViewModel
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
    
    // Nullable because it's only relevant for logged-in users in specific contexts
    public bool? IsWinning { get; set; }

    public string TimeRemaining => EndTime > DateTime.Now 
        ? $"{(EndTime - DateTime.Now).Days}d {(EndTime - DateTime.Now).Hours}h" 
        : "Expired";
}
