using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Models;

public class ContactMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = null!;

    public DateTime SentOn { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
