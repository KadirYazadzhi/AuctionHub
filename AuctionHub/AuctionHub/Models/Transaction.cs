using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuctionHub.Models;

public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = null!;

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    // Helps categorize: "Deposit", "Bid", "Refund", "Purchase"
    public string TransactionType { get; set; } = "General"; 
}
