using AuctionHub.Domain.Models;

namespace AuctionHub.Application.DTOs;

public class UserDetailsDto
{
    public string Id { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string DisplayName { get; set; } = null!;
    public decimal WalletBalance { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public List<AuctionDto> Auctions { get; set; } = new();
    public List<BidDto> Bids { get; set; } = new();
}
