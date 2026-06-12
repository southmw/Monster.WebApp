using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;

namespace Monster.WebApp.Services.Board;

public class CategoryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly CategoryAccessService _categoryAccessService;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        CategoryAccessService categoryAccessService,
        ILogger<CategoryService> logger)
    {
        _contextFactory = contextFactory;
        _categoryAccessService = categoryAccessService;
        _logger = logger;
    }

    public async Task<List<Category>> GetAllActiveCategoriesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 카테고리별 게시글 수 집계 (soft-delete 제외).
    /// 게시글이 없는 카테고리는 키가 없으므로 GetValueOrDefault로 0 처리할 것.
    /// </summary>
    public async Task<Dictionary<int, int>> GetPostCountsByCategoryAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
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
            .AsNoTracking()
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "카테고리 생성 실패: {Name} ({UrlSlug})", name, urlSlug);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "카테고리 수정 실패: Id {Id}", id);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "카테고리 삭제 실패: Id {Id}", id);
            return false;
        }
    }
}
