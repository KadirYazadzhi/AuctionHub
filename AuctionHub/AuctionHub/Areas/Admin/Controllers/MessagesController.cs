using AuctionHub.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class MessagesController : AdminBaseController
{
    private readonly AuctionHubDbContext _context;

    public MessagesController(AuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var messages = await _context.ContactMessages
            .OrderByDescending(m => m.SentOn)
            .ToListAsync();

        return View(messages);
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var message = await _context.ContactMessages.FindAsync(id);
        if (message != null)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await _context.ContactMessages.FindAsync(id);
        if (message != null)
        {
            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Message deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
