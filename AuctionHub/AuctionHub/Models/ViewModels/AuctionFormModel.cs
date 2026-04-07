using System.ComponentModel.DataAnnotations;
using AuctionHub.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AuctionHub.Models.ViewModels;

public class AuctionFormModel
{
    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string Description { get; set; } = null!;

    [Display(Name = "Upload Image")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Or Image URL")]
    public string? ImageUrl { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal StartPrice { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Minimum increase must be greater than 0.")]
    [Display(Name = "Minimum Bid Increase")]
    public decimal MinIncrease { get; set; }

    [Display(Name = "Buy It Now Price (Optional)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal? BuyItNowPrice { get; set; }

    [Display(Name = "Reserve Price (Optional)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Reserve price must be greater than 0.")]
    public decimal? ReservePrice { get; set; }

    // --- Dutch Auction ---
    public bool IsDutchAuction { get; set; }
    
    [Display(Name = "Price Drop Amount (€)")]
    public decimal? DutchDecrementAmount { get; set; }
    
    [Display(Name = "Price Drop Interval (Minutes)")]
    public int? DutchDecrementIntervalMinutes { get; set; }

    [Display(Name = "Bidding Participation Fee (€)")]
    public decimal? ParticipationFee { get; set; }

    [Required]
    [Display(Name = "Auction End Time")]
    public DateTime EndTime { get; set; }

    [Required]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    // --- Location Data ---
    [Required]
    [StringLength(50)]
    public string? Country { get; set; } = "Bulgaria";

    [Required]
    [StringLength(50)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? District { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }
    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public List<IFormFile> AdditionalImageFiles { get; set; } = new();
    public string? AdditionalImageUrlsJson { get; set; } 
    public List<AuctionImageDto> ExistingImages { get; set; } = new();
    public string? ImagesToRemoveIdsJson { get; set; }
    public bool ShouldPromote { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
}
