using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Models;

public class Bid
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime BidTime { get; set; }

    [Required]
    public string BidderId { get; set; } = null!;

    [ForeignKey(nameof(BidderId))]
    public virtual ApplicationUser Bidder { get; set; } = null!;

    [Required]
    public int AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;
}
