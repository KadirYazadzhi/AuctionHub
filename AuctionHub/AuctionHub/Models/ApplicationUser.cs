using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    public virtual ICollection<Auction> MyAuctions { get; set; } = new HashSet<Auction>();
    public virtual ICollection<Bid> MyBids { get; set; } = new HashSet<Bid>();
}
