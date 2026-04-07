namespace AuctionHub.Application.DTOs;

public class AuctionDetailsDto
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal StartPrice { get; set; }
    public decimal MinIncrease { get; set; }
    public decimal MinStep { get; set; } // Added: The calculated minimum next step
    public decimal? BuyItNowPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public bool ReservePriceMet => !ReservePrice.HasValue || CurrentPrice >= ReservePrice.Value;

    public bool IsDutchAuction { get; set; }
    public decimal? DutchDecrementAmount { get; set; }
    public int? DutchDecrementIntervalMinutes { get; set; }
    public DateTime? NextDutchDecrement { get; set; }

    public decimal? ParticipationFee { get; set; }
    public bool HasPaidParticipationFee { get; set; }
    public DateTime EndTime { get; set; }
    public string Category { get; set; } = null!;
    public int CategoryId { get; set; }
    
    // --- Location Data ---
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public List<AuctionImageDto> Images { get; set; } = new();
    public string Seller { get; set; } = null!;
    public string SellerId { get; set; } = null!;
    public Guid SellerPublicId { get; set; }
    public double SellerRating { get; set; }
    public int SellerReviewCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsDelivered { get; set; }
    public bool IsSettled { get; set; }
    public bool IsDisputed { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsWatched { get; set; }
    public bool? IsWinning { get; set; }
    public decimal? CurrentAutoBidLimit { get; set; }
    public string? WinnerId { get; set; }
    public List<BidDto> Bids { get; set; } = new();
    public List<PrivateOfferDto> PrivateOffers { get; set; } = new();
    public List<CommentDto> Comments { get; set; } = new();
}
