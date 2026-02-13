using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

[Authorize(Roles = "Administrator")]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _categoryService.GetAllAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryDto category)
    {
        if (ModelState.IsValid)
        {
            await _categoryService.CreateAsync(category);
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var category = await _categoryService.GetByIdAsync(id.Value);
        if (category == null) return NotFound();
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryDto category)
    {
        if (id != category.Id) return NotFound();

        if (ModelState.IsValid)
        {
            await _categoryService.UpdateAsync(category);
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    public async Task<IActionResult> Delete(int? id)
    {
         if (id == null) return NotFound();
        var category = await _categoryService.GetByIdAsync(id.Value);
        if (category == null) return NotFound();

        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category != null)
        {
            if (category.AuctionsCount > 0)
            {
                TempData["Error"] = "Cannot delete category with auctions.";
                return RedirectToAction(nameof(Index));
            }
            await _categoryService.DeleteAsync(id);
        }
        return RedirectToAction(nameof(Index));
    }
}
