using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using System.Security.Claims;

namespace Monster.WebApp.Tests;

/// <summary>
/// SQLite in-memory 기반 IDbContextFactory 구현.
/// 연결을 열어둔 채 유지해야 in-memory DB가 보존되므로 IDisposable로 수명을 관리한다.
/// EnsureCreated()로 OnModelCreating의 HasData 시드(역할 3종, 카테고리 3개)가 함께 생성된다.
/// </summary>
public sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// 테스트용 IHttpContextAccessor 생성 헬퍼.
/// AuthService/RoleService는 HttpContext.User의 클레임만 읽으므로 DefaultHttpContext로 충분하다.
/// </summary>
public static class TestHttpContext
{
    /// <summary>비로그인(익명) 사용자 컨텍스트</summary>
    public static IHttpContextAccessor Anonymous()
        => new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

    /// <summary>지정한 사용자 ID와 역할로 인증된 컨텍스트</summary>
    public static IHttpContextAccessor AuthenticatedAs(int userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
        };
        return new HttpContextAccessor { HttpContext = httpContext };
    }
}
