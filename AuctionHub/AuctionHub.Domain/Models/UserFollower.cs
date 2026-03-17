using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class UserFollower
{
    [Required]
    public string FollowerId { get; set; } = null!;

    [ForeignKey(nameof(FollowerId))]
    public virtual ApplicationUser Follower { get; set; } = null!;

    [Required]
    public string SellerId { get; set; } = null!;

    [ForeignKey(nameof(SellerId))]
    public virtual ApplicationUser Seller { get; set; } = null!;

    public DateTime FollowedOn { get; set; } = DateTime.UtcNow;
}
