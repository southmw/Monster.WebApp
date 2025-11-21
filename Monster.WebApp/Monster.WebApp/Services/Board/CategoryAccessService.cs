using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class CategoryAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;
    private readonly RoleService _roleService;

    public CategoryAccessService(ApplicationDbContext context, AuthService authService, RoleService roleService)
    {
        _context = context;
        _authService = authService;
        _roleService = roleService;
    }

    public async Task<bool> CanAccessCategoryAsync(int categoryId, int? userId = null)
    {
        var category = await _context.Categories.FindAsync(categoryId);
        if (category == null || !category.IsActive)
        {
            return false;
        }

        // If public board, everyone can access
        if (category.IsPublic && !category.RequireAuth)
        {
            return true;
        }

        // Use current user if not specified
        userId ??= _authService.GetCurrentUserId();

        // If requires auth and user is not logged in
        if (category.RequireAuth && userId == null)
        {
            return false;
        }

        // Admins can access all boards
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, "Admin"))
        {
            return true;
        }

        // Public board but requires login - logged in users can access
        if (category.IsPublic && category.RequireAuth && userId != null)
        {
            return true;
        }

        // Check specific category access permissions
        if (userId != null)
        {
            // Check user-specific access
            var hasUserAccess = await _context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId && ca.UserId == userId);

            if (hasUserAccess)
            {
                return true;
            }

            // Check role-based access
            var userRoleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId.Value)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var hasRoleAccess = await _context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId && ca.RoleId != null && userRoleIds.Contains(ca.RoleId.Value));

            if (hasRoleAccess)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanWriteToCategoryAsync(int categoryId, int? userId = null)
    {
        // Must be able to access first
        if (!await CanAccessCategoryAsync(categoryId, userId))
        {
            return false;
        }

        userId ??= _authService.GetCurrentUserId();

        // Admins can write to all boards
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, "Admin"))
        {
            return true;
        }

        var category = await _context.Categories.FindAsync(categoryId);
        if (category == null)
        {
            return false;
        }

        // Public boards - anyone who can access can write
        if (category.IsPublic)
        {
            return true;
        }

        // Check write permissions
        if (userId != null)
        {
            var hasWriteAccess = await _context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId &&
                               (ca.UserId == userId || (ca.RoleId != null && _context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == ca.RoleId))) &&
                               (ca.AccessType == AccessType.Write || ca.AccessType == AccessType.Manage));

            return hasWriteAccess;
        }

        return false;
    }

    public async Task<bool> CanManageCategoryAsync(int categoryId, int? userId = null)
    {
        userId ??= _authService.GetCurrentUserId();

        // Admins can manage all boards
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, "Admin"))
        {
            return true;
        }

        // Check manage permissions
        if (userId != null)
        {
            var hasManageAccess = await _context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId &&
                               (ca.UserId == userId || (ca.RoleId != null && _context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == ca.RoleId))) &&
                               ca.AccessType == AccessType.Manage);

            return hasManageAccess;
        }

        return false;
    }

    public async Task<bool> GrantAccessAsync(int categoryId, AccessType accessType, int? userId = null, int? roleId = null)
    {
        if (userId == null && roleId == null)
        {
            return false;
        }

        // Check if access already exists
        var existingAccess = await _context.CategoryAccesses
            .FirstOrDefaultAsync(ca => ca.CategoryId == categoryId &&
                                      ca.UserId == userId &&
                                      ca.RoleId == roleId);

        if (existingAccess != null)
        {
            // Update access type
            existingAccess.AccessType = accessType;
        }
        else
        {
            // Create new access
            _context.CategoryAccesses.Add(new CategoryAccess
            {
                CategoryId = categoryId,
                UserId = userId,
                RoleId = roleId,
                AccessType = accessType,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RevokeAccessAsync(int categoryId, int? userId = null, int? roleId = null)
    {
        var access = await _context.CategoryAccesses
            .FirstOrDefaultAsync(ca => ca.CategoryId == categoryId &&
                                      ca.UserId == userId &&
                                      ca.RoleId == roleId);

        if (access == null)
        {
            return false;
        }

        _context.CategoryAccesses.Remove(access);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Category>> GetAccessibleCategoriesAsync(int? userId = null)
    {
        userId ??= _authService.GetCurrentUserId();

        // Get all active categories
        var allCategories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var accessibleCategories = new List<Category>();

        foreach (var category in allCategories)
        {
            if (await CanAccessCategoryAsync(category.Id, userId))
            {
                accessibleCategories.Add(category);
            }
        }

        return accessibleCategories;
    }
}
