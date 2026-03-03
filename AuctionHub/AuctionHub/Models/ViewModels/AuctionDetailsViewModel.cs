using System.ComponentModel.DataAnnotations;

namespace AuctionHub.Models.ViewModels;

public class AuctionDetailsViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal StartPrice { get; set; }

    public decimal MinIncrease { get; set; }
    
    public decimal? BuyItNowPrice { get; set; }

    public DateTime EndTime { get; set; }

    public string Category { get; set; } = null!;
    public List<string> Images { get; set; } = new();

    public string Seller { get; set; } = null!;
    public string SellerId { get; set; } = null!;
        public double SellerRating { get; set; }
        public int SellerReviewCount { get; set; }
            public bool IsActive { get; set; }
            public bool IsDelivered { get; set; }
            public bool IsSettled { get; set; }
            public bool IsDisputed { get; set; }
            public bool IsSuspended { get; set; }

    public bool IsWatched { get; set; } // Added

    public bool? IsWinning { get; set; }

    public string? WinnerId { get; set; }

    public decimal? CurrentAutoBidLimit { get; set; }

    public IEnumerable<BidViewModel> Bids { get; set; } = new List<BidViewModel>();

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Bid amount must be greater than 0.")]
    public decimal NewBidAmount { get; set; }
}

public class BidViewModel
{
    public decimal Amount { get; set; }
    public DateTime BidTime { get; set; }
    public string Bidder { get; set; } = null!;
}