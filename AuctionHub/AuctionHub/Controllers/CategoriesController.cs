using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Controllers;

public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        return View(await _categoryService.GetAllAsync());
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryDto category)
    {
// ... (rest of class)
        if (ModelState.IsValid)
        {
            await _categoryService.CreateAsync(category);
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var category = await _categoryService.GetByIdAsync(id.Value);
        if (category == null) return NotFound();
        return View(category);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
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

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int? id)
    {
         if (id == null) return NotFound();
        var category = await _categoryService.GetByIdAsync(id.Value);
        if (category == null) return NotFound();

        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Administrator")]
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
