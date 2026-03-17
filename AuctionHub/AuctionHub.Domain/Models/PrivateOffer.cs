using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class PrivateOffer
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;

    [Required]
    public string BuyerId { get; set; } = null!;

    [ForeignKey(nameof(BuyerId))]
    public virtual ApplicationUser Buyer { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

    [Required]
    public DateTime CreatedOn { get; set; }
}
