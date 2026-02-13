namespace AuctionHub.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int AuctionsCount { get; set; }
}
