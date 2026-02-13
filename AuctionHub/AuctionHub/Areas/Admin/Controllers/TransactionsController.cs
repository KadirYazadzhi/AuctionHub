using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Areas.Admin.Controllers;

public class TransactionsController : AdminBaseController
{
    private readonly IWalletService _walletService;

    public TransactionsController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    public async Task<IActionResult> Index()
    {
        var transactions = await _walletService.GetAllTransactionsAsync(100);

        return View(transactions);
    }
}
