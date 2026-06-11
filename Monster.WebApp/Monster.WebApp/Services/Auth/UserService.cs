using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Auth;

public class UserService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    public UserService(IDbContextFactory<ApplicationDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<User> Users, int TotalCount)> GetUsersPagedAsync(int page = 1, int pageSize = 20)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role);

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> UpdateUserAsync(int userId, string? email = null, string? displayName = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(email))
        {
            user.Email = email;
        }

        if (!string.IsNullOrEmpty(displayName))
        {
            user.DisplayName = displayName;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetUserActiveStatusAsync(int userId, bool isActive)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // 쿠키 사용자 유효성 캐시 제거 — 비활성화가 5분 캐시 지연 없이 다음 요청부터 반영되도록
        _cache.Remove($"{AppConstants.CacheKeys.UserValidPrefix}{userId}");
        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var lowerSearchTerm = searchTerm.ToLower();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.Username.ToLower().Contains(lowerSearchTerm) ||
                       u.Email.ToLower().Contains(lowerSearchTerm) ||
                       u.DisplayName.ToLower().Contains(lowerSearchTerm))
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetTotalUserCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users.CountAsync();
    }

    public async Task<int> GetActiveUserCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users.CountAsync(u => u.IsActive);
    }

    public async Task<List<User>> GetRecentUsersAsync(int count = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> UpdateUserProfileAsync(int userId, string email, string displayName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Check if email is already used by another user
        var existingUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Id != userId);
        if (existingUser != null)
        {
            return false;
        }

        user.Email = email;
        user.DisplayName = displayName;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 관리자 전용: 사용자 비밀번호 강제 리셋
    /// </summary>
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<UserStatistics> GetUserStatisticsAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var commentCount = await context.Comments
            .CountAsync(c => c.UserId == userId && !c.IsDeleted);

        // 게시글 수/조회수/추천수를 DB에서 단일 쿼리로 집계 (전체 게시글 로드 방지)
        var postStats = await context.Posts
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalViews = g.Sum(p => p.ViewCount),
                TotalVotes = g.Sum(p => p.VoteCount)
            })
            .FirstOrDefaultAsync();

        return new UserStatistics
        {
            PostCount = postStats?.Count ?? 0,
            CommentCount = commentCount,
            TotalViews = postStats?.TotalViews ?? 0,
            TotalVotes = postStats?.TotalVotes ?? 0
        };
    }
}

public class UserStatistics
{
    public int PostCount { get; set; }
    public int CommentCount { get; set; }
    public int TotalViews { get; set; }
    public int TotalVotes { get; set; }
}
