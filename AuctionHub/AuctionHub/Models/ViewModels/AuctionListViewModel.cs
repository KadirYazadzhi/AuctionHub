namespace AuctionHub.Models.ViewModels;

public class AuctionListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = null!;
    public bool IsActive { get; set; }
    public string TimeRemaining => EndTime > DateTime.Now 
        ? $"{(EndTime - DateTime.Now).Days}d {(EndTime - DateTime.Now).Hours}h" 
        : "Expired";
}
