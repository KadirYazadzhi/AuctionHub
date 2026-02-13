namespace AuctionHub.Application.DTOs;

public class TransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = null!;
    public string User { get; set; } = null!;
}
