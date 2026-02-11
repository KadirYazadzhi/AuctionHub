using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Models;

public class Auction
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = null!;

    [Required]
    public string Description { get; set; } = null!;

    public string? ImageUrl { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal StartPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinIncrease { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BuyItNowPrice { get; set; }

    [Required]
    public DateTime CreatedOn { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSuspended { get; set; } = false;

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    [Required]
    public string SellerId { get; set; } = null!;

    [ForeignKey(nameof(SellerId))]
    public virtual ApplicationUser Seller { get; set; } = null!;

    [Required]
    public int CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Bid> Bids { get; set; } = new HashSet<Bid>();
}
