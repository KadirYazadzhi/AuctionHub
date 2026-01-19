using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Models;

public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = null!;

    public virtual ICollection<Auction> Auctions { get; set; } = new HashSet<Auction>();
}
