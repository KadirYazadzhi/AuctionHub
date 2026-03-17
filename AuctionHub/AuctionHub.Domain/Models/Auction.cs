using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class Auction
{
    [Key]
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

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

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReservePrice { get; set; }

    [Required]
    public DateTime CreatedOn { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    // --- Location Data ---
    [StringLength(50)]
    public string? Country { get; set; } = "Bulgaria";
    [StringLength(50)]
    public string? City { get; set; }
    [StringLength(50)]
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsActive { get; set; } = true;
        public bool IsPromoted { get; set; } = false;
        public bool IsSuspended { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public bool IsSettled { get; set; } = false;
        public bool IsDisputed { get; set; } = false;

    public int ViewCount { get; set; } = 0;

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

    public virtual ICollection<AuctionImage> Images { get; set; } = new HashSet<AuctionImage>();
    public virtual ICollection<Bid> Bids { get; set; } = new HashSet<Bid>();
}
