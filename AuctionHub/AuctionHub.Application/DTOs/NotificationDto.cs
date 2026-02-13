namespace AuctionHub.Application.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string Message { get; set; } = null!;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
