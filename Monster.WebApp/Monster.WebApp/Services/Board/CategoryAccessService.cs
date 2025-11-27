using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Board;

public class CategoryAccessService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;
    private readonly RoleService _roleService;

    public CategoryAccessService(IDbContextFactory<ApplicationDbContext> contextFactory, AuthService authService, RoleService roleService)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _roleService = roleService;
    }

    public async Task<bool> CanAccessCategoryAsync(int categoryId, int? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var category = await context.Categories.FindAsync(categoryId);
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
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, AppConstants.Roles.Admin))
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
            var hasUserAccess = await context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId && ca.UserId == userId);

            if (hasUserAccess)
            {
                return true;
            }

            // Check role-based access
            var userRoleIds = await context.UserRoles
                .Where(ur => ur.UserId == userId.Value)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var hasRoleAccess = await context.CategoryAccesses
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
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, AppConstants.Roles.Admin))
        {
            return true;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var category = await context.Categories.FindAsync(categoryId);
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
            var hasWriteAccess = await context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId &&
                               (ca.UserId == userId || (ca.RoleId != null && context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == ca.RoleId))) &&
                               (ca.AccessType == AccessType.Write || ca.AccessType == AccessType.Manage));

            return hasWriteAccess;
        }

        return false;
    }

    public async Task<bool> CanManageCategoryAsync(int categoryId, int? userId = null)
    {
        userId ??= _authService.GetCurrentUserId();

        // Admins can manage all boards
        if (userId != null && await _roleService.HasRoleAsync(userId.Value, AppConstants.Roles.Admin))
        {
            return true;
        }

        // Check manage permissions
        if (userId != null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var hasManageAccess = await context.CategoryAccesses
                .AnyAsync(ca => ca.CategoryId == categoryId &&
                               (ca.UserId == userId || (ca.RoleId != null && context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == ca.RoleId))) &&
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

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check if access already exists
        var existingAccess = await context.CategoryAccesses
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
            context.CategoryAccesses.Add(new CategoryAccess
            {
                CategoryId = categoryId,
                UserId = userId,
                RoleId = roleId,
                AccessType = accessType,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RevokeAccessAsync(int categoryId, int? userId = null, int? roleId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var access = await context.CategoryAccesses
            .FirstOrDefaultAsync(ca => ca.CategoryId == categoryId &&
                                      ca.UserId == userId &&
                                      ca.RoleId == roleId);

        if (access == null)
        {
            return false;
        }

        context.CategoryAccesses.Remove(access);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 접근 가능한 카테고리 목록 조회 (N+1 쿼리 최적화)
    /// </summary>
    public async Task<List<Category>> GetAccessibleCategoriesAsync(int? userId = null)
    {
        userId ??= _authService.GetCurrentUserId();

        await using var context = await _contextFactory.CreateDbContextAsync();

        // 1. 모든 활성 카테고리와 접근 권한을 한 번에 로드
        var allCategories = await context.Categories
            .Where(c => c.IsActive)
            .Include(c => c.CategoryAccesses)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        // 2. 사용자 역할 정보를 한 번에 로드 (로그인한 경우)
        List<int>? userRoleIds = null;
        bool isAdmin = false;

        if (userId.HasValue)
        {
            userRoleIds = await context.UserRoles
                .Where(ur => ur.UserId == userId.Value)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            // Admin 역할 확인
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == AppConstants.Roles.Admin);
            if (adminRole != null && userRoleIds.Contains(adminRole.Id))
            {
                isAdmin = true;
            }
        }

        // 3. 메모리에서 필터링
        return allCategories
            .Where(c => CanAccessCategoryInMemory(c, userId, userRoleIds, isAdmin))
            .ToList();
    }

    /// <summary>
    /// 메모리에서 카테고리 접근 권한 체크 (DB 쿼리 없음)
    /// </summary>
    private bool CanAccessCategoryInMemory(
        Category category,
        int? userId,
        List<int>? userRoleIds,
        bool isAdmin)
    {
        // 비활성 카테고리
        if (!category.IsActive)
            return false;

        // 공개 + 인증 불필요 → 모든 사용자 접근 가능
        if (category.IsPublic && !category.RequireAuth)
            return true;

        // 인증 필요한데 로그인 안함
        if (category.RequireAuth && userId == null)
            return false;

        // 관리자는 모든 카테고리 접근 가능
        if (isAdmin)
            return true;

        // 공개 + 인증 필요 + 로그인됨 → 접근 가능
        if (category.IsPublic && category.RequireAuth && userId != null)
            return true;

        // 특정 권한 체크 (비공개 카테고리)
        if (userId != null && category.CategoryAccesses != null)
        {
            // 사용자별 접근 권한
            if (category.CategoryAccesses.Any(ca => ca.UserId == userId))
                return true;

            // 역할별 접근 권한
            if (userRoleIds != null && category.CategoryAccesses
                .Any(ca => ca.RoleId != null && userRoleIds.Contains(ca.RoleId.Value)))
                return true;
        }

        return false;
    }
}
