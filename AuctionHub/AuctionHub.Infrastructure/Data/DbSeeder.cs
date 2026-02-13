using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionHub.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AuctionHubDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // 1. Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new Category { Name = "Electronics", IconClass = "bi-laptop" },
                new Category { Name = "Collectibles & Art", IconClass = "bi-palette" },
                new Category { Name = "Fashion", IconClass = "bi-bag-heart" },
                new Category { Name = "Home & Garden", IconClass = "bi-house-heart" },
                new Category { Name = "Auto Parts & Accessories", IconClass = "bi-car-front" },
                new Category { Name = "Toys & Hobbies", IconClass = "bi-joystick" },
                new Category { Name = "Sports", IconClass = "bi-bicycle" },
                new Category { Name = "Books & Movies", IconClass = "bi-book" }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // 2. Seed Roles
        string adminRole = "Administrator";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        // 3. Seed Admin User
        string adminEmail = "admin@auctionhub.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin",
                WalletBalance = 1000000m,
                RowVersion = new byte[8]
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
            }
        }
        else
        {
            // Ensure existing admin has the role
            if (!await userManager.IsInRoleAsync(adminUser, adminRole))
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
            }
        }
    }
}