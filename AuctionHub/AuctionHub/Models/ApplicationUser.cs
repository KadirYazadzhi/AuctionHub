using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WalletBalance { get; set; } = 0.00m;

    public virtual ICollection<Auction> MyAuctions { get; set; } = new HashSet<Auction>();
    public virtual ICollection<Bid> MyBids { get; set; } = new HashSet<Bid>();
}
