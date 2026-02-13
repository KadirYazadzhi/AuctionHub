namespace AuctionHub.Application.DTOs;

public class ContactMessageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime SentOn { get; set; }
    public bool IsRead { get; set; }
}
