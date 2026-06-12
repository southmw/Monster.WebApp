using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services.Board;

namespace Monster.WebApp.Tests;

/// <summary>
/// CategoryService — 카테고리 목록 조회·게시글 수 집계 테스트.
/// HasData 시드: 카테고리 Id 1=자유게시판, 2=질문게시판, 3=정보공유 (모두 활성)
/// </summary>
public class CategoryServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private const int FreeCategoryId = 1;      // 글 2건 + soft-delete 1건
    private const int QuestionsCategoryId = 2; // 글 1건
    private const int InfoCategoryId = 3;      // 글 없음

    private readonly int _inactiveCategoryId;

    public CategoryServiceTests()
    {
        using var context = _factory.CreateDbContext();

        var inactiveCategory = new Category
        {
            Name = "보관됨", UrlSlug = "archived", Description = "비활성 게시판",
            DisplayOrder = 99, IsActive = false
        };
        context.Categories.Add(inactiveCategory);

        context.Posts.AddRange(
            NewPost(FreeCategoryId, "자유 1"),
            NewPost(FreeCategoryId, "자유 2"),
            NewPost(FreeCategoryId, "자유 삭제됨", isDeleted: true),
            NewPost(QuestionsCategoryId, "질문 1"));
        context.SaveChanges();

        _inactiveCategoryId = inactiveCategory.Id;
    }

    private static Post NewPost(int categoryId, string title, bool isDeleted = false) => new()
    {
        CategoryId = categoryId,
        Title = title,
        Content = "본문",
        AuthorNickname = "tester",
        IsDeleted = isDeleted
    };

    private CategoryService CreateService()
    {
        var accessor = TestHttpContext.Anonymous();
        var authService = new AuthService(_factory, accessor, _cache);
        var roleService = new RoleService(_factory, accessor);
        var accessService = new CategoryAccessService(_factory, authService, roleService);
        return new CategoryService(_factory, accessService, NullLogger<CategoryService>.Instance);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    // ---- GetPostCountsByCategoryAsync ----

    [Fact]
    public async Task 카테고리별_게시글_수를_정확히_집계한다()
    {
        var service = CreateService();
        var counts = await service.GetPostCountsByCategoryAsync();

        Assert.Equal(2, counts[FreeCategoryId]);
        Assert.Equal(1, counts[QuestionsCategoryId]);
    }

    [Fact]
    public async Task 삭제된_게시글은_집계에서_제외된다()
    {
        var service = CreateService();
        var counts = await service.GetPostCountsByCategoryAsync();

        Assert.Equal(2, counts[FreeCategoryId]); // soft-delete 1건 제외
    }

    [Fact]
    public async Task 게시글이_없는_카테고리는_키가_없고_GetValueOrDefault로_0이_된다()
    {
        var service = CreateService();
        var counts = await service.GetPostCountsByCategoryAsync();

        Assert.False(counts.ContainsKey(InfoCategoryId));
        Assert.Equal(0, counts.GetValueOrDefault(InfoCategoryId));
    }

    // ---- GetAllCategoriesAsync / GetAllActiveCategoriesAsync ----

    [Fact]
    public async Task 전체_카테고리_조회는_비활성_카테고리를_포함한다()
    {
        var service = CreateService();
        var categories = await service.GetAllCategoriesAsync();

        Assert.Contains(categories, c => c.Id == _inactiveCategoryId);
    }

    [Fact]
    public async Task 활성_카테고리_조회는_비활성_카테고리를_제외한다()
    {
        var service = CreateService();
        var categories = await service.GetAllActiveCategoriesAsync();

        Assert.DoesNotContain(categories, c => c.Id == _inactiveCategoryId);
    }
}
