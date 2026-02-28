using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Domain.Models;

public class SystemSetting
{
    [Key]
    public string Key { get; set; } = null!; // e.g., "PromotionFee", "MinBidStep"

    [Required]
    public string Value { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
