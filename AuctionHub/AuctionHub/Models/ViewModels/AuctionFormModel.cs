using System.ComponentModel.DataAnnotations;
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
    [Url]
    public string? ImageUrl { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal StartPrice { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Minimum increase must be greater than 0.")]
    [Display(Name = "Minimum Bid Increase")]
    public decimal MinIncrease { get; set; }

    [Required]
    [Display(Name = "Auction End Time")]
    public DateTime EndTime { get; set; }

    [Required]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
}
