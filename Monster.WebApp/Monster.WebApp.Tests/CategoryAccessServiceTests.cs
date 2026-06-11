using Microsoft.Extensions.Caching.Memory;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// CategoryAccessService — 카테고리 접근/쓰기 권한 매트릭스 테스트.
/// HasData 시드: 역할 Id 1=Admin, 2=SubAdmin, 3=User / 카테고리 Id 1~3(공개, 인증 불필요)
/// </summary>
public class CategoryAccessServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private const int AdminRoleId = 1;
    private const int SubAdminRoleId = 2;
    private const int PublicCategoryId = 1; // 시드: 자유게시판 (IsPublic, !RequireAuth)

    private readonly int _privateCategoryId;     // 비공개 (IsPublic=false, RequireAuth=true)
    private readonly int _authOnlyCategoryId;    // 공개 + 로그인 필요 (IsPublic=true, RequireAuth=true)
    private readonly int _adminUserId;
    private readonly int _normalUserId;          // 아무 권한 부여 없음
    private readonly int _grantedUserId;         // 비공개 카테고리에 Read 권한 부여
    private readonly int _writerUserId;          // 비공개 카테고리에 Write 권한 부여
    private readonly int _subAdminUserId;        // SubAdmin 역할 — 역할 단위 Read 권한 부여 대상

    public CategoryAccessServiceTests()
    {
        using var context = _factory.CreateDbContext();

        var privateCategory = new Category
        {
            Name = "비공개", UrlSlug = "private", Description = "비공개 게시판",
            DisplayOrder = 10, IsActive = true, IsPublic = false, RequireAuth = true
        };
        var authOnlyCategory = new Category
        {
            Name = "회원전용", UrlSlug = "members", Description = "로그인 필요 게시판",
            DisplayOrder = 11, IsActive = true, IsPublic = true, RequireAuth = true
        };
        context.Categories.AddRange(privateCategory, authOnlyCategory);

        var admin = NewUser("admin-t");
        var normal = NewUser("normal-t");
        var granted = NewUser("granted-t");
        var writer = NewUser("writer-t");
        var subAdmin = NewUser("subadmin-t");
        context.Users.AddRange(admin, normal, granted, writer, subAdmin);
        context.SaveChanges();

        context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = AdminRoleId });
        context.UserRoles.Add(new UserRole { UserId = subAdmin.Id, RoleId = SubAdminRoleId });

        context.CategoryAccesses.AddRange(
            new CategoryAccess { CategoryId = privateCategory.Id, UserId = granted.Id, AccessType = AccessType.Read },
            new CategoryAccess { CategoryId = privateCategory.Id, UserId = writer.Id, AccessType = AccessType.Write },
            new CategoryAccess { CategoryId = privateCategory.Id, RoleId = SubAdminRoleId, AccessType = AccessType.Read });
        context.SaveChanges();

        _privateCategoryId = privateCategory.Id;
        _authOnlyCategoryId = authOnlyCategory.Id;
        _adminUserId = admin.Id;
        _normalUserId = normal.Id;
        _grantedUserId = granted.Id;
        _writerUserId = writer.Id;
        _subAdminUserId = subAdmin.Id;
    }

    private static User NewUser(string name) => new()
    {
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "irrelevant",
        DisplayName = name,
        IsActive = true
    };

    private CategoryAccessService CreateService()
    {
        // userId를 명시적으로 넘겨 테스트하므로 HttpContext는 익명으로 충분
        var accessor = TestHttpContext.Anonymous();
        var authService = new AuthService(_factory, accessor, _cache);
        var roleService = new RoleService(_factory, accessor);
        return new CategoryAccessService(_factory, authService, roleService);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    // ---- CanAccessCategoryAsync ----

    [Fact]
    public async Task 공개_카테고리는_익명도_접근할_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanAccessCategoryAsync(PublicCategoryId));
    }

    [Fact]
    public async Task 로그인_필요_카테고리는_익명이_접근할_수_없다()
    {
        var service = CreateService();
        Assert.False(await service.CanAccessCategoryAsync(_authOnlyCategoryId));
    }

    [Fact]
    public async Task 로그인_필요_카테고리는_로그인_사용자가_접근할_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanAccessCategoryAsync(_authOnlyCategoryId, _normalUserId));
    }

    [Fact]
    public async Task 비공개_카테고리는_권한_없는_사용자가_접근할_수_없다()
    {
        var service = CreateService();
        Assert.False(await service.CanAccessCategoryAsync(_privateCategoryId, _normalUserId));
    }

    [Fact]
    public async Task 비공개_카테고리는_사용자별_권한이_있으면_접근할_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanAccessCategoryAsync(_privateCategoryId, _grantedUserId));
    }

    [Fact]
    public async Task 비공개_카테고리는_역할별_권한이_있으면_접근할_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanAccessCategoryAsync(_privateCategoryId, _subAdminUserId));
    }

    [Fact]
    public async Task 관리자는_비공개_카테고리에_접근할_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanAccessCategoryAsync(_privateCategoryId, _adminUserId));
    }

    [Fact]
    public async Task 존재하지_않는_카테고리는_접근할_수_없다()
    {
        var service = CreateService();
        Assert.False(await service.CanAccessCategoryAsync(99999, _adminUserId));
    }

    // ---- CanWriteToCategoryAsync ----

    [Fact]
    public async Task 공개_카테고리는_접근_가능한_사용자가_쓸_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanWriteToCategoryAsync(PublicCategoryId, _normalUserId));
    }

    [Fact]
    public async Task 비공개_카테고리는_Read_권한만으로는_쓸_수_없다()
    {
        var service = CreateService();
        Assert.False(await service.CanWriteToCategoryAsync(_privateCategoryId, _grantedUserId));
    }

    [Fact]
    public async Task 비공개_카테고리는_Write_권한이_있으면_쓸_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanWriteToCategoryAsync(_privateCategoryId, _writerUserId));
    }

    [Fact]
    public async Task 관리자는_비공개_카테고리에_쓸_수_있다()
    {
        var service = CreateService();
        Assert.True(await service.CanWriteToCategoryAsync(_privateCategoryId, _adminUserId));
    }
}
