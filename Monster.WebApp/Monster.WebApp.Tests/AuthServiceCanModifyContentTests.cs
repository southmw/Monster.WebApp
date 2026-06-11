using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// AuthService.CanModifyContent — 게시글/댓글 수정·삭제 권한 검증 로직 테스트.
/// (관리자 통과 / 로그인 작성물은 본인만 / 익명 작성물은 비밀번호 검증)
/// </summary>
public class AuthServiceCanModifyContentTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private AuthService CreateService(IHttpContextAccessor accessor)
        => new(_factory, accessor, _cache);

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public void 관리자는_타인의_로그인_작성물을_수정할_수_있다()
    {
        var service = CreateService(TestHttpContext.AuthenticatedAs(99, AppConstants.Roles.Admin));

        Assert.True(service.CanModifyContent(ownerUserId: 1, authorPasswordHash: null, providedPassword: null));
    }

    [Fact]
    public void 관리자는_익명_작성물을_비밀번호_없이_수정할_수_있다()
    {
        var service = CreateService(TestHttpContext.AuthenticatedAs(99, AppConstants.Roles.Admin));

        Assert.True(service.CanModifyContent(ownerUserId: null, authorPasswordHash: BCrypt.Net.BCrypt.HashPassword("secret"), providedPassword: null));
    }

    [Fact]
    public void 본인_작성물은_수정할_수_있다()
    {
        var service = CreateService(TestHttpContext.AuthenticatedAs(5, AppConstants.Roles.User));

        Assert.True(service.CanModifyContent(ownerUserId: 5, authorPasswordHash: null, providedPassword: null));
    }

    [Fact]
    public void 타인_작성물은_수정할_수_없다()
    {
        var service = CreateService(TestHttpContext.AuthenticatedAs(5, AppConstants.Roles.User));

        Assert.False(service.CanModifyContent(ownerUserId: 7, authorPasswordHash: null, providedPassword: null));
    }

    [Fact]
    public void 비로그인_사용자는_로그인_작성물을_수정할_수_없다()
    {
        var service = CreateService(TestHttpContext.Anonymous());

        Assert.False(service.CanModifyContent(ownerUserId: 5, authorPasswordHash: null, providedPassword: null));
    }

    [Fact]
    public void 익명_작성물은_올바른_비밀번호로_수정할_수_있다()
    {
        var service = CreateService(TestHttpContext.Anonymous());
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-pw");

        Assert.True(service.CanModifyContent(ownerUserId: null, authorPasswordHash: hash, providedPassword: "correct-pw"));
    }

    [Fact]
    public void 익명_작성물은_틀린_비밀번호로_수정할_수_없다()
    {
        var service = CreateService(TestHttpContext.Anonymous());
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-pw");

        Assert.False(service.CanModifyContent(ownerUserId: null, authorPasswordHash: hash, providedPassword: "wrong-pw"));
    }

    [Fact]
    public void 익명_작성물은_비밀번호_미입력_시_수정할_수_없다()
    {
        var service = CreateService(TestHttpContext.Anonymous());
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-pw");

        Assert.False(service.CanModifyContent(ownerUserId: null, authorPasswordHash: hash, providedPassword: null));
        Assert.False(service.CanModifyContent(ownerUserId: null, authorPasswordHash: hash, providedPassword: ""));
    }

    [Fact]
    public void 익명_작성물에_해시가_없으면_수정할_수_없다()
    {
        var service = CreateService(TestHttpContext.Anonymous());

        Assert.False(service.CanModifyContent(ownerUserId: null, authorPasswordHash: null, providedPassword: "any-pw"));
    }
}
