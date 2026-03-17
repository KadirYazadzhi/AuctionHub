using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class AuctionParticipant
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    [Required]
    public DateTime PaidOn { get; set; }
}
