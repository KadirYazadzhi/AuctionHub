using AuctionHub.Domain.Models;

namespace AuctionHub.Application.DTOs;

public class UserDetailsDto
{
    public string Id { get; set; } = null!;
    public Guid PublicId { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? AboutMe { get; set; }
    public string DisplayName { get; set; } = null!;
    public decimal WalletBalance { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool IsShadowBanned { get; set; }
    
    public int ActiveBidsCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalEarned { get; set; }
    public double WinRate { get; set; }
    public List<DailyActivityDto> PersonalActivity { get; set; } = new();

    public double AverageRating { get; set; }
    public bool IsTopSeller { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();
    public List<AuctionDto> Auctions { get; set; } = new();
    public List<BidDto> Bids { get; set; } = new();
}
