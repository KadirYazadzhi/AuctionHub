using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionHub.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrator")]
public abstract class AdminBaseController : Controller
{
}
