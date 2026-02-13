using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task CreateAsync(CategoryDto model);
    Task UpdateAsync(CategoryDto model);
    Task DeleteAsync(int id);
}
