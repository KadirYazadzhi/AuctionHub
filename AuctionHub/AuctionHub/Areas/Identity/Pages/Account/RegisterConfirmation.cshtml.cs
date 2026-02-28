using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Threading.Tasks;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AuctionHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _sender;
        private readonly IConfiguration _config;

        public RegisterConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender sender, IConfiguration config)
        {
            _userManager = userManager;
            _sender = sender;
            _config = config;
        }

        public string Email { get; set; } = null!;

        public bool DisplayConfirmAccountLink { get; set; }

        public string? EmailConfirmationUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(string email, string? returnUrl = null)
        {
            if (email == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound($"Unable to load user with email '{email}'.");
            }

            Email = email;
            
            // If Mailtrap token is missing, show the link for easy development testing
            var apiToken = _config["EmailSettings:ApiToken"];
            DisplayConfirmAccountLink = string.IsNullOrEmpty(apiToken) || apiToken == "YOUR_MAILTRAP_TOKEN";

            if (DisplayConfirmAccountLink)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                EmailConfirmationUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);
            }

            return Page();
        }
    }
}
