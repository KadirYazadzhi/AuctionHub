namespace AuctionHub.Application.DTOs;

public class ChatSessionDto
{
    public bool IsGlobal { get; set; }
    public int? AuctionId { get; set; }
    public string? AuctionTitle { get; set; }
    public string? OtherUserId { get; set; }
    public Guid OtherUserPublicId { get; set; }
    public string? OtherUserName { get; set; }
    public string? OtherUserAvatar { get; set; }
    public string LastMessage { get; set; } = null!;
    public DateTime LastMessageTime { get; set; }
}