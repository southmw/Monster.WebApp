using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Services.Auth;

public class RoleService
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public RoleService(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<List<Role>> GetUserRolesAsync(int userId)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role)
            .ToListAsync();
    }

    public async Task<bool> AssignRoleAsync(int userId, int roleId)
    {
        // Check if already assigned
        if (await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId))
        {
            return false;
        }

        _context.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveRoleAsync(int userId, int roleId)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole == null)
        {
            return false;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasRoleAsync(int userId, string roleName)
    {
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName);
    }

    public async Task<bool> IsInRoleAsync(string roleName)
    {
        var userId = _authService.GetCurrentUserId();
        if (userId == null)
        {
            return false;
        }

        return await HasRoleAsync(userId.Value, roleName);
    }

    public async Task<bool> IsAdminAsync()
    {
        return await IsInRoleAsync("Admin");
    }

    public async Task<bool> IsSubAdminOrHigherAsync()
    {
        return await IsInRoleAsync("Admin") || await IsInRoleAsync("SubAdmin");
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _context.Roles.ToListAsync();
    }
}
