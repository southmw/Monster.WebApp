using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// 프로필 활동 목록용 사용자별 조회 — PostService.GetPostsByUserAsync / CommentService.GetCommentsByUserAsync 테스트.
/// HasData 시드: 카테고리 Id 1~3
/// </summary>
public class UserContentQueryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private readonly int _userId;
    private readonly int _otherUserId;

    public UserContentQueryTests()
    {
        using var context = _factory.CreateDbContext();

        var user = NewUser("me");
        var other = NewUser("other");
        context.Users.AddRange(user, other);
        context.SaveChanges();
        _userId = user.Id;
        _otherUserId = other.Id;

        var myPost = NewPost(user.Id, "내 글");
        var myDeletedPost = NewPost(user.Id, "삭제된 내 글");
        myDeletedPost.IsDeleted = true;
        var othersPost = NewPost(other.Id, "남의 글");
        var deletedParentPost = NewPost(other.Id, "삭제될 글");
        deletedParentPost.IsDeleted = true;
        context.Posts.AddRange(myPost, myDeletedPost, othersPost, deletedParentPost);
        context.SaveChanges();

        context.Comments.AddRange(
            NewComment(myPost.Id, user.Id, "<p>내 댓글 1</p>"),
            NewComment(othersPost.Id, user.Id, "<p>내 댓글 2</p>"),
            NewComment(othersPost.Id, other.Id, "<p>남의 댓글</p>"),
            NewComment(deletedParentPost.Id, user.Id, "<p>삭제된 글의 내 댓글</p>"));
        var deletedComment = NewComment(myPost.Id, user.Id, "<p>삭제된 내 댓글</p>");
        deletedComment.IsDeleted = true;
        context.Comments.Add(deletedComment);
        context.SaveChanges();
    }

    private static User NewUser(string name) => new()
    {
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "irrelevant",
        DisplayName = name,
        IsActive = true
    };

    private static Post NewPost(int userId, string title) => new()
    {
        CategoryId = 1,
        UserId = userId,
        Title = title,
        Content = "<p>본문</p>",
        IsHtml = true,
        AuthorNickname = "닉네임"
    };

    private static Comment NewComment(int postId, int userId, string content) => new()
    {
        PostId = postId,
        UserId = userId,
        Content = content,
        IsHtml = true,
        AuthorNickname = "닉네임"
    };

    private (PostService Posts, CommentService Comments) CreateServices()
    {
        var accessor = TestHttpContext.Anonymous();
        var authService = new AuthService(_factory, accessor, _cache);
        var sanitizer = new ContentSanitizer();
        var fileUpload = new FileUploadService(new TestWebHostEnvironment(), NullLogger<FileUploadService>.Instance);
        var notification = new NotificationService(_factory);
        var postService = new PostService(_factory, authService, accessor, sanitizer, fileUpload);
        var commentService = new CommentService(_factory, authService, sanitizer, fileUpload, notification, NullLogger<CommentService>.Instance);
        return (postService, commentService);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task 본인_게시글만_조회되고_삭제_글은_제외된다()
    {
        var (postService, _) = CreateServices();

        var (posts, totalCount) = await postService.GetPostsByUserAsync(_userId);

        Assert.Equal(1, totalCount);
        var post = Assert.Single(posts);
        Assert.Equal("내 글", post.Title);
        Assert.NotNull(post.Category); // 목록의 카테고리 칩/링크용
    }

    [Fact]
    public async Task 본인_댓글만_조회되고_삭제_댓글과_삭제된_글의_댓글은_제외된다()
    {
        var (_, commentService) = CreateServices();

        var (comments, totalCount) = await commentService.GetCommentsByUserAsync(_userId);

        Assert.Equal(2, totalCount);
        Assert.All(comments, c => Assert.Equal(_userId, c.UserId));
        Assert.DoesNotContain(comments, c => c.Content.Contains("삭제된"));
        Assert.All(comments, c =>
        {
            Assert.NotNull(c.Post);          // 「글 제목」에 작성 표시용
            Assert.NotNull(c.Post.Category); // 링크 slug용
        });
    }

    [Fact]
    public async Task 사용자별_조회는_페이지네이션된다()
    {
        using (var context = _factory.CreateDbContext())
        {
            for (var i = 0; i < 12; i++)
            {
                context.Posts.Add(NewPost(_otherUserId, $"글 {i}"));
            }
            context.SaveChanges();
        }

        var (postService, _) = CreateServices();
        var (page1, totalCount) = await postService.GetPostsByUserAsync(_otherUserId, page: 1, pageSize: 10);
        var (page2, _) = await postService.GetPostsByUserAsync(_otherUserId, page: 2, pageSize: 10);

        Assert.Equal(13, totalCount); // 기존 1 + 추가 12 (삭제 글 제외)
        Assert.Equal(10, page1.Count);
        Assert.Equal(3, page2.Count);
    }
}
