using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class AuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string AdminId { get; set; } = null!;

    [ForeignKey(nameof(AdminId))]
    public virtual ApplicationUser Admin { get; set; } = null!;

    [Required]
    public string Action { get; set; } = null!; // e.g., "Updated User Balance", "Suspended Auction"

    public string? EntityName { get; set; } // e.g., "Auction", "User"

    public string? EntityId { get; set; }

    public string? Details { get; set; } // JSON or descriptive string of changes

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }
}
