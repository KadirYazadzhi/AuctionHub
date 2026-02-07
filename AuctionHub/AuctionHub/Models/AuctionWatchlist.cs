using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Models;

public class AuctionWatchlist
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    [Required]
    public int AuctionId { get; set; }
    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;

    public DateTime AddedOn { get; set; } = DateTime.UtcNow;
}
