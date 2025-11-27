using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class PostService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PostService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuthService authService,
        IHttpContextAccessor httpContextAccessor)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetPostsByCategoryAsync(
        int categoryId,
        int page = 1,
        int pageSize = 20,
        string? searchQuery = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Posts
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(p => p.Title.Contains(searchQuery) || p.Content.Contains(searchQuery));
        }

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (posts, totalCount);
    }

    public async Task<Post?> GetPostByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Post> CreatePostAsync(Post post, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Set UserId if user is authenticated
        var userId = _authService.GetCurrentUserId();
        if (userId != null)
        {
            post.UserId = userId;
            post.AuthorPassword = null; // Authenticated users don't need password
        }
        else
        {
            // Anonymous post requires password
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required for anonymous posts");

            post.UserId = null;
            post.AuthorPassword = BCrypt.Net.BCrypt.HashPassword(password);
        }

        post.CreatedAt = DateTime.UtcNow;

        context.Posts.Add(post);
        await context.SaveChangesAsync();

        return post;
    }

    public async Task<bool> UpdatePostAsync(int id, Post updatedPost, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        // 관리자는 모든 게시글 수정 가능
        if (_authService.IsAdmin())
        {
            post.Title = updatedPost.Title;
            post.Content = updatedPost.Content;
            post.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return true;
        }

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (post.UserId != null)
        {
            // Authenticated post - must be owner
            if (userId != post.UserId)
                return false;
        }
        else
        {
            // Anonymous post - verify password
            if (string.IsNullOrWhiteSpace(password) || post.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, post.AuthorPassword))
                return false;
        }

        post.Title = updatedPost.Title;
        post.Content = updatedPost.Content;
        post.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePostAsync(int id, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        // 관리자는 모든 게시글 삭제 가능
        if (_authService.IsAdmin())
        {
            post.IsDeleted = true;
            await context.SaveChangesAsync();
            return true;
        }

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (post.UserId != null)
        {
            // Authenticated post - must be owner
            if (userId != post.UserId)
                return false;
        }
        else
        {
            // Anonymous post - verify password
            if (string.IsNullOrWhiteSpace(password) || post.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, post.AuthorPassword))
                return false;
        }

        post.IsDeleted = true;
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 조회수 증가 (세션 기반 중복 방지)
    /// </summary>
    public async Task<bool> IncrementViewCountAsync(int id)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return false;

        // 세션에서 이미 조회한 게시글인지 확인
        var viewedPostsKey = $"viewed_post_{id}";
        var hasViewed = httpContext.Session.GetString(viewedPostsKey);

        if (hasViewed != null)
            return false; // 이미 조회함

        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post != null && !post.IsDeleted)
        {
            post.ViewCount++;
            await context.SaveChangesAsync();

            // 세션에 조회 기록 저장
            httpContext.Session.SetString(viewedPostsKey, "1");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 게시글 추천 (중복 방지)
    /// </summary>
    public async Task<(bool Success, string Message)> VotePostAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return (false, "게시글을 찾을 수 없습니다.");

        var userId = _authService.GetCurrentUserId();
        var ipAddress = GetClientIpAddress();

        // 중복 투표 체크
        bool hasVoted;
        if (userId != null)
        {
            // 로그인 사용자: UserId로 체크
            hasVoted = await context.PostVotes
                .AnyAsync(v => v.PostId == id && v.UserId == userId);
        }
        else
        {
            // 익명 사용자: IP로 체크
            hasVoted = await context.PostVotes
                .AnyAsync(v => v.PostId == id && v.IpAddress == ipAddress && v.UserId == null);
        }

        if (hasVoted)
            return (false, "이미 추천한 게시글입니다.");

        // 투표 기록 저장
        var vote = new PostVote
        {
            PostId = id,
            UserId = userId,
            IpAddress = userId == null ? ipAddress : null,
            VoteValue = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.PostVotes.Add(vote);
        post.VoteCount++;
        await context.SaveChangesAsync();

        return (true, "추천되었습니다.");
    }

    /// <summary>
    /// 사용자가 게시글에 이미 투표했는지 확인
    /// </summary>
    public async Task<bool> HasVotedAsync(int postId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var userId = _authService.GetCurrentUserId();
        var ipAddress = GetClientIpAddress();

        if (userId != null)
        {
            return await context.PostVotes
                .AnyAsync(v => v.PostId == postId && v.UserId == userId);
        }
        else
        {
            return await context.PostVotes
                .AnyAsync(v => v.PostId == postId && v.IpAddress == ipAddress && v.UserId == null);
        }
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // X-Forwarded-For 헤더 확인 (프록시/로드밸런서 뒤에 있는 경우)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    public async Task<int> GetTotalPostCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts.CountAsync(p => !p.IsDeleted);
    }

    public async Task<bool> UpdatePostContentAsync(int postId, string content)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(postId);
        if (post == null || post.IsDeleted)
            return false;

        post.Content = content;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Post>> GetRecentPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Post>> GetPopularPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive)
            .OrderByDescending(p => p.ViewCount)
            .ThenByDescending(p => p.VoteCount)
            .Take(count)
            .ToListAsync();
    }
}
