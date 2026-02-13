using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class MessagesController : AdminBaseController
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    public async Task<IActionResult> Index()
    {
        var messages = await _messageService.GetAllAsync();

        return View(messages);
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _messageService.MarkReadAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _messageService.DeleteAsync(id);
        TempData["Success"] = "Message deleted.";
        return RedirectToAction(nameof(Index));
    }
}
