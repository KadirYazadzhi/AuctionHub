using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Application.DTOs;

public class ChatMessageDto
{
    public int Id { get; set; }
    
    public string SenderId { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    
    public string? ReceiverId { get; set; }
    public string? ReceiverName { get; set; }
    
    public int? AuctionId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = null!;

    public DateTime SentOn { get; set; }
    
    public bool IsGlobal { get; set; }
}
