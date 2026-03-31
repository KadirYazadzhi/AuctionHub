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
        
        // --- MANUAL SCHEMA UPDATE: Add 'Status' column to Transactions table ---
        // Since we cannot run migrations in this environment, we apply it via raw SQL.
        try 
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.columns 
                               WHERE object_id = OBJECT_ID(N'[Transactions]') 
                               AND name = N'Status')
                BEGIN
                    ALTER TABLE [Transactions] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'Completed';
                END
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating database schema: {ex.Message}");
        }

        // --- CLEANUP DUPLICATES: Fix for 'Sequence contains more than one element' ---
        try
        {
            // Use NormalizedEmail because that's what Identity uses for FindByEmailAsync
            var duplicateNormalizedEmails = await context.Users
                .Where(u => u.NormalizedEmail != null)
                .GroupBy(u => u.NormalizedEmail)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            foreach (var normEmail in duplicateNormalizedEmails)
            {
                var users = await context.Users
                    .Where(u => u.NormalizedEmail == normEmail)
                    .OrderBy(u => u.Id)
                    .ToListAsync();

                if (users.Count > 1)
                {
                    // Keep the first one, delete the rest
                    context.Users.RemoveRange(users.Skip(1));
                }
            }
            await context.SaveChangesAsync();
            
            // Also check for duplicate Usernames
            var duplicateNormalizedUserNames = await context.Users
                .Where(u => u.NormalizedUserName != null)
                .GroupBy(u => u.NormalizedUserName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            foreach (var normName in duplicateNormalizedUserNames)
            {
                var users = await context.Users
                    .Where(u => u.NormalizedUserName == normName)
                    .OrderBy(u => u.Id)
                    .ToListAsync();

                if (users.Count > 1)
                {
                    context.Users.RemoveRange(users.Skip(1));
                }
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up duplicate users: {ex.Message}");
        }

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
        string userRole = "User";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        if (!await roleManager.RoleExistsAsync(userRole))
        {
            await roleManager.CreateAsync(new IdentityRole(userRole));
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

        // 4. Seed System Settings
        if (!await context.SystemSettings.AnyAsync())
        {
            var settings = new List<SystemSetting>
            {
                new SystemSetting { Key = "CommissionRate", Value = "5", LastUpdated = DateTime.UtcNow },
                new SystemSetting { Key = "MinWithdrawal", Value = "10", LastUpdated = DateTime.UtcNow },
                new SystemSetting { Key = "PromotionFee", Value = "1.99", LastUpdated = DateTime.UtcNow }
            };
            await context.SystemSettings.AddRangeAsync(settings);
            await context.SaveChangesAsync();
        }
    }
}