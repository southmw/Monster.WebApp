using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Shared;
using System.Security.Claims;

namespace Monster.WebApp.Services.Auth;

public class RoleService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RoleService(IDbContextFactory<ApplicationDbContext> contextFactory, IHttpContextAccessor httpContextAccessor)
    {
        _contextFactory = contextFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Roles.ToListAsync();
    }

    public async Task<bool> AssignRoleAsync(int userId, int roleId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check if assignment already exists
        if (await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId))
        {
            return false;
        }

        context.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveRoleAsync(int userId, int roleId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var userRole = await context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole == null)
        {
            return false;
        }

        context.UserRoles.Remove(userRole);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Role>> GetUserRolesAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync();
    }

    public async Task<bool> IsInRoleAsync(string roleName)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return httpContext.User.IsInRole(roleName);
    }

    public async Task<bool> HasRoleAsync(int userId, string roleName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName);
    }

    public async Task<bool> IsAdminAsync()
    {
        return await IsInRoleAsync(AppConstants.Roles.Admin);
    }

    public async Task<bool> IsSubAdminOrHigherAsync()
    {
        return await IsInRoleAsync(AppConstants.Roles.Admin) || await IsInRoleAsync(AppConstants.Roles.SubAdmin);
    }

    public async Task<bool> CanManageUserAsync(int targetUserId)
    {
        // Admin can manage everyone
        if (await IsAdminAsync())
        {
            return true;
        }

        // SubAdmin can manage Users but not other Admins/SubAdmins
        if (await IsSubAdminOrHigherAsync())
        {
            var targetIsAdmin = await HasRoleAsync(targetUserId, AppConstants.Roles.Admin);
            var targetIsSubAdmin = await HasRoleAsync(targetUserId, AppConstants.Roles.SubAdmin);

            return !targetIsAdmin && !targetIsSubAdmin;
        }

        return false;
    }
}
