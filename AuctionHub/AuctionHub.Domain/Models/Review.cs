using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class Review
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;

    [Required]
    public string ReviewerId { get; set; } = null!;

    [ForeignKey(nameof(ReviewerId))]
    public virtual ApplicationUser Reviewer { get; set; } = null!;

    [Required]
    public string TargetUserId { get; set; } = null!;

    [ForeignKey(nameof(TargetUserId))]
    public virtual ApplicationUser TargetUser { get; set; } = null!;

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(500)]
    public string Comment { get; set; } = null!;

    [Required]
    public DateTime CreatedOn { get; set; }
}
