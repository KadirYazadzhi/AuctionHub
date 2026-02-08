using System.ComponentModel.DataAnnotations;
using AuctionHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuctionHub.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }

        public string Username { get; set; } = null!;
        public string? CurrentProfilePictureUrl { get; set; }

        [TempData]
        public string StatusMessage { get; set; } = null!;

        [BindProperty]
        public InputModel Input { get; set; } = null!;

        public class InputModel
        {
            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            [Required]
            [StringLength(50, MinimumLength = 2)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; } = null!;

            [Required]
            [StringLength(50, MinimumLength = 2)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; } = null!;

            [Display(Name = "Profile Picture")]
            public IFormFile? ProfilePicture { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName!;
            CurrentProfilePictureUrl = user.ProfilePictureUrl;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? ""
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // Image Upload Logic
            if (Input.ProfilePicture != null)
            {
                if (Input.ProfilePicture.Length > 2 * 1024 * 1024) // 2MB limit
                {
                    ModelState.AddModelError("Input.ProfilePicture", "The profile picture must be less than 2MB.");
                    await LoadAsync(user);
                    return Page();
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(Input.ProfilePicture.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.ProfilePicture", "Invalid file type. Only .jpg, .jpeg, .png, and .webp are allowed.");
                    await LoadAsync(user);
                    return Page();
                }

                // Delete old picture if exists and is local
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl) && user.ProfilePictureUrl.StartsWith("/images/profiles/"))
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                // Save new picture
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await Input.ProfilePicture.CopyToAsync(fileStream);
                }

                user.ProfilePictureUrl = $"/images/profiles/{uniqueFileName}";
                await _userManager.UpdateAsync(user); // Save URL to DB
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            if (Input.FirstName != user.FirstName || Input.LastName != user.LastName)
            {
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}