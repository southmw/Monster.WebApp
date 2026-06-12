using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// PostService.SearchPostsAsync — 통합 검색의 매칭 규칙과 접근 카테고리 선필터 테스트.
/// HasData 시드: 카테고리 Id 1~3 (모두 공개)
/// </summary>
public class PostServiceSearchTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private readonly int _privateCategoryId;

    public PostServiceSearchTests()
    {
        using var context = _factory.CreateDbContext();

        var privateCategory = new Category
        {
            Name = "비공개", UrlSlug = "secret", Description = "비공개 게시판",
            DisplayOrder = 10, IsActive = true, IsPublic = false, RequireAuth = true
        };
        context.Categories.Add(privateCategory);
        context.SaveChanges();
        _privateCategoryId = privateCategory.Id;

        context.Posts.AddRange(
            NewPost(1, "사과의 효능", "<p>본문</p>", searchText: "본문"),
            NewPost(1, "일상 글", "<p>오늘 <strong>사과</strong>를 먹었다</p>", searchText: "오늘 사과를 먹었다"),
            NewPost(2, "레거시 글", "평문에 사과 포함", searchText: null, isHtml: false),
            NewPost(1, "삭제된 사과 글", "<p>사과</p>", searchText: "사과", isDeleted: true),
            NewPost(_privateCategoryId, "비공개 사과 글", "<p>사과</p>", searchText: "사과"),
            NewPost(2, "무관한 글", "<p>바나나</p>", searchText: "바나나"));
        context.SaveChanges();
    }

    private static Post NewPost(int categoryId, string title, string content,
        string? searchText, bool isHtml = true, bool isDeleted = false) => new()
    {
        CategoryId = categoryId,
        Title = title,
        Content = content,
        SearchText = searchText,
        IsHtml = isHtml,
        IsDeleted = isDeleted,
        AuthorNickname = "작성자"
    };

    private PostService CreateService()
    {
        var accessor = TestHttpContext.Anonymous();
        return new PostService(
            _factory,
            new AuthService(_factory, accessor, _cache),
            accessor,
            new ContentSanitizer(),
            new FileUploadService(new TestWebHostEnvironment(), NullLogger<FileUploadService>.Instance));
    }

    /// <summary>시드 공개 카테고리(1~3)만 접근 가능한 상황</summary>
    private static readonly int[] PublicCategoryIds = { 1, 2, 3 };

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task 제목과_본문에서_검색어가_매칭된다()
    {
        var service = CreateService();

        var (posts, totalCount) = await service.SearchPostsAsync("사과", PublicCategoryIds);

        // 제목 매칭 + SearchText 매칭 + 레거시 Content 매칭 (삭제 글·비공개 카테고리 제외)
        Assert.Equal(3, totalCount);
        Assert.Contains(posts, p => p.Title == "사과의 효능");
        Assert.Contains(posts, p => p.Title == "일상 글");
        Assert.Contains(posts, p => p.Title == "레거시 글");
    }

    [Fact]
    public async Task 삭제된_글은_검색되지_않는다()
    {
        var service = CreateService();

        var (posts, _) = await service.SearchPostsAsync("사과", PublicCategoryIds);

        Assert.DoesNotContain(posts, p => p.IsDeleted);
    }

    [Fact]
    public async Task 접근_불가_카테고리의_글은_검색되지_않는다()
    {
        var service = CreateService();

        var (posts, _) = await service.SearchPostsAsync("사과", PublicCategoryIds);
        Assert.DoesNotContain(posts, p => p.CategoryId == _privateCategoryId);

        // 접근 목록에 비공개 카테고리가 포함되면 검색됨 (선필터가 결과를 결정)
        var (withPrivate, _) = await service.SearchPostsAsync("사과", new[] { 1, 2, 3, _privateCategoryId });
        Assert.Contains(withPrivate, p => p.CategoryId == _privateCategoryId);
    }

    [Fact]
    public async Task 빈_검색어나_빈_카테고리_목록은_빈_결과를_반환한다()
    {
        var service = CreateService();

        var (byEmptyQuery, count1) = await service.SearchPostsAsync("  ", PublicCategoryIds);
        var (byEmptyCategories, count2) = await service.SearchPostsAsync("사과", Array.Empty<int>());

        Assert.Empty(byEmptyQuery);
        Assert.Equal(0, count1);
        Assert.Empty(byEmptyCategories);
        Assert.Equal(0, count2);
    }

    [Fact]
    public async Task 페이지네이션이_TotalCount와_정합한다()
    {
        var service = CreateService();

        var (page1, totalCount) = await service.SearchPostsAsync("사과", PublicCategoryIds, page: 1, pageSize: 2);
        var (page2, _) = await service.SearchPostsAsync("사과", PublicCategoryIds, page: 2, pageSize: 2);

        Assert.Equal(3, totalCount);
        Assert.Equal(2, page1.Count);
        Assert.Single(page2);
    }

    [Fact]
    public async Task 검색_결과에_카테고리가_로드된다()
    {
        var service = CreateService();

        var (posts, _) = await service.SearchPostsAsync("사과", PublicCategoryIds);

        Assert.All(posts, p => Assert.NotNull(p.Category));
    }
}
