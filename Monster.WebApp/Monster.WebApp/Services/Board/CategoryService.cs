using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;

namespace Monster.WebApp.Services.Board;

public class CategoryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly CategoryAccessService _categoryAccessService;

    public CategoryService(IDbContextFactory<ApplicationDbContext> contextFactory, CategoryAccessService categoryAccessService)
    {
        _contextFactory = contextFactory;
        _categoryAccessService = categoryAccessService;
    }

    public async Task<List<Category>> GetAllActiveCategoriesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories
            .Include(c => c.Posts)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAccessibleCategoriesAsync(int? userId = null)
    {
        return await _categoryAccessService.GetAccessibleCategoriesAsync(userId);
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories.FindAsync(id);
    }

    public async Task<Category?> GetCategoryByUrlSlugAsync(string urlSlug)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories
            .FirstOrDefaultAsync(c => c.UrlSlug == urlSlug && c.IsActive);
    }

    public async Task<bool> CreateCategoryAsync(string name, string urlSlug, string? description, int displayOrder)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var category = new Category
            {
                Name = name,
                UrlSlug = urlSlug,
                Description = description ?? string.Empty,
                DisplayOrder = displayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            var category = await context.Categories.FindAsync(id);
            if (category == null) return false;

            category.Name = name;
            category.UrlSlug = urlSlug;
            category.Description = description ?? string.Empty;
            category.DisplayOrder = displayOrder;
            category.IsActive = isActive;

            await context.SaveChangesAsync();
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            var category = await context.Categories.FindAsync(id);
            if (category == null) return false;

            // Check if there are posts in this category
            var hasPost = await context.Posts.AnyAsync(p => p.CategoryId == id && !p.IsDeleted);
            if (hasPost) return false; // Cannot delete category with posts

            context.Categories.Remove(category);
            await context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
