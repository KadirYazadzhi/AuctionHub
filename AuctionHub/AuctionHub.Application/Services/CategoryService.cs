using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AuctionHub.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IAuctionHubDbContext _context;
    private readonly IDistributedCache _cache;
    private const string CategoriesCacheKey = "Categories_List";

    public CategoryService(IAuctionHubDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        // 1. Try to get from Cache
        var cachedCategories = await _cache.GetStringAsync(CategoriesCacheKey);
        if (!string.IsNullOrEmpty(cachedCategories))
        {
            return JsonSerializer.Deserialize<IEnumerable<CategoryDto>>(cachedCategories)!;
        }

        // 2. If not in cache, get from DB
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                AuctionsCount = c.Auctions.Count
            })
            .ToListAsync();

        // 3. Save to Cache for 30 minutes
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        await _cache.SetStringAsync(CategoriesCacheKey, JsonSerializer.Serialize(categories), options);

        return categories;
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Auctions)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null) return null;

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            AuctionsCount = category.Auctions.Count
        };
    }

    public async Task CreateAsync(CategoryDto model)
    {
        var category = new Category
        {
            Name = model.Name
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CategoriesCacheKey); // Invalidate cache
    }

    public async Task UpdateAsync(CategoryDto model)
    {
        var category = await _context.Categories.FindAsync(model.Id);
        if (category != null)
        {
            category.Name = model.Name;
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(CategoriesCacheKey); // Invalidate cache
        }
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(CategoriesCacheKey); // Invalidate cache
        }
    }
}
