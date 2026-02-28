using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class UserReport
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ReporterId { get; set; } = null!;

    [ForeignKey(nameof(ReporterId))]
    public virtual ApplicationUser Reporter { get; set; } = null!;

    public string? ReportedUserId { get; set; }

    [ForeignKey(nameof(ReportedUserId))]
    public virtual ApplicationUser? ReportedUser { get; set; }

    public int? ReportedAuctionId { get; set; }

    [ForeignKey(nameof(ReportedAuctionId))]
    public virtual Auction? ReportedAuction { get; set; }

    [Required]
    [StringLength(100)]
    public string Reason { get; set; } = null!; // e.g., "Fraud", "Offensive Content"

    [Required]
    [StringLength(2000)]
    public string Details { get; set; } = null!;

    public bool IsResolved { get; set; } = false;

    public string? AdminNotes { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
