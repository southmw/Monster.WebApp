using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;

namespace Monster.WebApp.Services.Board;

public class CategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly CategoryAccessService _categoryAccessService;

    public CategoryService(ApplicationDbContext context, CategoryAccessService categoryAccessService)
    {
        _context = context;
        _categoryAccessService = categoryAccessService;
    }

    public async Task<List<Category>> GetAllActiveCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAccessibleCategoriesAsync(int? userId = null)
    {
        return await _categoryAccessService.GetAccessibleCategoriesAsync(userId);
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        return await _context.Categories.FindAsync(id);
    }

    public async Task<Category?> GetCategoryByUrlSlugAsync(string urlSlug)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.UrlSlug == urlSlug && c.IsActive);
    }

    public async Task<bool> CreateCategoryAsync(string name, string urlSlug, string? description, int displayOrder)
    {
        try
        {
            var category = new Category
            {
                Name = name,
                UrlSlug = urlSlug,
                Description = description ?? string.Empty,
                DisplayOrder = displayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateCategoryAsync(int id, string name, string urlSlug, string? description, int displayOrder, bool isActive)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            category.Name = name;
            category.UrlSlug = urlSlug;
            category.Description = description ?? string.Empty;
            category.DisplayOrder = displayOrder;
            category.IsActive = isActive;

            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            // Check if there are posts in this category
            var hasPost = await _context.Posts.AnyAsync(p => p.CategoryId == id && !p.IsDeleted);
            if (hasPost) return false; // Cannot delete category with posts

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
