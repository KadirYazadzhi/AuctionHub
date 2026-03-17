namespace AuctionHub.Application.DTOs;

public class FollowerDto
{
    public string Id { get; set; } = null!;
    public Guid PublicId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? ProfilePictureUrl { get; set; }
}
