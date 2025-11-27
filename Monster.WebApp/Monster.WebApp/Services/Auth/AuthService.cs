using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Auth;

public class AuthService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;

    // 로그인 시도 제한 설정
    private const int MaxLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    public async Task<User?> RegisterAsync(string username, string email, string password, string displayName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check if username or email already exists
        if (await context.Users.AnyAsync(u => u.Username == username || u.Email == email))
        {
            return null;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assign default "User" role
        var userRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == AppConstants.Roles.User);
        if (userRole != null)
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id,
                AssignedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>
    /// 로그인 처리 (시도 제한 포함)
    /// </summary>
    public async Task<(User? User, string? ErrorMessage)> LoginAsync(string username, string password)
    {
        var clientIp = GetClientIpAddress();
        var lockoutKey = $"login_lockout_{username}_{clientIp}";
        var attemptsKey = $"login_attempts_{username}_{clientIp}";

        // 잠금 상태 확인
        if (_cache.TryGetValue(lockoutKey, out DateTime lockoutEnd))
        {
            var remaining = lockoutEnd - DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
            {
                return (null, $"로그인이 일시적으로 차단되었습니다. {Math.Ceiling(remaining.TotalMinutes)}분 후에 다시 시도해주세요.");
            }
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // 실패 횟수 증가
            var attempts = _cache.GetOrCreate(attemptsKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = LockoutDuration;
                return 0;
            });

            attempts++;
            _cache.Set(attemptsKey, attempts, LockoutDuration);

            if (attempts >= MaxLoginAttempts)
            {
                // 잠금 설정
                _cache.Set(lockoutKey, DateTime.UtcNow.Add(LockoutDuration), LockoutDuration);
                _cache.Remove(attemptsKey);
                return (null, $"로그인 시도 횟수를 초과했습니다. {LockoutDuration.TotalMinutes}분 후에 다시 시도해주세요.");
            }

            var remainingAttempts = MaxLoginAttempts - attempts;
            return (null, $"사용자명 또는 비밀번호가 올바르지 않습니다. (남은 시도: {remainingAttempts}회)");
        }

        // 로그인 성공 - 시도 횟수 초기화
        _cache.Remove(attemptsKey);
        _cache.Remove(lockoutKey);

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("DisplayName", user.DisplayName)
        };

        // Add role claims
        foreach (var userRole in user.UserRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        return (user, null);
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return "unknown";

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            return null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public int? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }

        return null;
    }

    public bool IsAuthenticated()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.User?.Identity?.IsAuthenticated == true;
    }

    public string? GetCurrentUserDisplayName()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var displayNameClaim = httpContext.User.FindFirst("DisplayName");
        return displayNameClaim?.Value;
    }

    public bool IsAdmin()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        return httpContext.User.IsInRole(AppConstants.Roles.Admin);
    }
}
