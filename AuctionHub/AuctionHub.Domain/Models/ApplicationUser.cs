using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

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

    [StringLength(500)]
    public string? AboutMe { get; set; }

    public bool IsShadowBanned { get; set; } = false;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

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

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Auction> MyAuctions { get; set; } = new HashSet<Auction>();
    public virtual ICollection<Bid> MyBids { get; set; } = new HashSet<Bid>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new HashSet<Transaction>();
    public virtual ICollection<AuctionWatchlist> Watchlist { get; set; } = new HashSet<AuctionWatchlist>();
    public virtual ICollection<Review> ReceivedReviews { get; set; } = new HashSet<Review>();

    [NotMapped]
    public double AverageRating => ReceivedReviews.Any() ? ReceivedReviews.Average(r => r.Rating) : 0;

    [NotMapped]
    public bool IsTopSeller => ReceivedReviews.Count >= 5 && AverageRating >= 4.8;
}
