using AuctionHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AuctionHubDbContext context)
    {
        if (await context.Categories.AnyAsync())
        {
            return;
        }

        var categories = new List<Category>
        {
            new Category { Name = "Electronics" },
            new Category { Name = "Collectibles & Art" },
            new Category { Name = "Fashion" },
            new Category { Name = "Home & Garden" },
            new Category { Name = "Auto Parts & Accessories" },
            new Category { Name = "Toys & Hobbies" },
            new Category { Name = "Sports" },
            new Category { Name = "Books & Movies" }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }
}
