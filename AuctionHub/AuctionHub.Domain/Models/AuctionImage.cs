using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class AuctionImage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Url { get; set; } = null!;

    public string? PublicId { get; set; } // Used for Cloudinary deletion

    [Required]
    public int AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public virtual Auction Auction { get; set; } = null!;
}
