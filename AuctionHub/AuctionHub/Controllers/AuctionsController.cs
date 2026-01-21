using AuctionHub.Data;
using AuctionHub.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize]
public class AuctionsController : Controller
{
    private readonly AuctionHubDbContext _context;

    public AuctionsController(AuctionHubDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new AuctionFormModel
        {
            Categories = await _context.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync(),
            EndTime = DateTime.Now.AddDays(7)
        };

        return View(model);
    }
}
