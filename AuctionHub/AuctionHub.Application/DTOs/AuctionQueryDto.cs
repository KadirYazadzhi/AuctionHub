namespace AuctionHub.Application.DTOs;

public class AuctionQueryDto
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public string? SortOrder { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 9;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Status { get; set; }
    public string? CurrentUserId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? MaxDistance { get; set; }
    public string? Username { get; set; }
}
