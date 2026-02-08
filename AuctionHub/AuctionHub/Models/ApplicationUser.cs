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

    [StringLength(200)]
    public string? ProfilePictureUrl { get; set; }

    [NotMapped]
    public string DisplayName 
    {
        get 
        {
            var name = !string.IsNullOrEmpty(UserName) ? UserName : Email;
            if (string.IsNullOrEmpty(name)) return "Unknown";
            
            if (name.Contains('@'))
            {
                return name.Split('@')[0];
            }
            return name;
        }
    }

    public virtual ICollection<Auction> MyAuctions { get; set; } = new HashSet<Auction>();
    public virtual ICollection<Bid> MyBids { get; set; } = new HashSet<Bid>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new HashSet<Transaction>();
    public virtual ICollection<AuctionWatchlist> Watchlist { get; set; } = new HashSet<AuctionWatchlist>();
}
