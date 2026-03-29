using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Domain.Models;

public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = null!;

    [ForeignKey(nameof(SenderId))]
    public ApplicationUser Sender { get; set; } = null!;

    public string? ReceiverId { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser? Receiver { get; set; }

    public int? AuctionId { get; set; }

    [ForeignKey(nameof(AuctionId))]
    public Auction? Auction { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; set; } = null!;

    public DateTime SentOn { get; set; } = DateTime.UtcNow;

    public bool IsGlobal { get; set; } = true;

    // Track visibility for each participant independently
    public bool IsHiddenForSender { get; set; } = false;
    public bool IsHiddenForReceiver { get; set; } = false;
}
