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

    public async Task<IActionResult> Index(int? page)
    {
        int pageSize = 15;
        int pageNumber = page ?? 1;

        var transactions = await _walletService.GetPaginatedTransactionsAsync(pageNumber, pageSize);

        return View(transactions);
    }
}
