namespace AuctionHub.Application.DTOs;

public class CommentDto
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public string UserId { get; set; } = null!;
    public string UserDisplayName { get; set; } = null!;
    public string? UserProfilePictureUrl { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
}
