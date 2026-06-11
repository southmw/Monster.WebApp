using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Board;

public class PostService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ContentSanitizer _contentSanitizer;
    private readonly FileUploadService _fileUploadService;

    public PostService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuthService authService,
        IHttpContextAccessor httpContextAccessor,
        ContentSanitizer contentSanitizer,
        FileUploadService fileUploadService)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
        _contentSanitizer = contentSanitizer;
        _fileUploadService = fileUploadService;
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetPostsByCategoryAsync(
        int categoryId,
        int page = 1,
        int pageSize = 20,
        string? searchQuery = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            // HTML 글(IsHtml=true)은 태그 제거본(SearchText)으로, 레거시 평문 글은 Content로 검색
            query = query.Where(p => p.Title.Contains(searchQuery) || (p.SearchText ?? p.Content).Contains(searchQuery));
        }

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.PinnedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (posts, totalCount);
    }

    public async Task<Post?> GetPostByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Post> CreatePostAsync(Post post, string? password = null)
    {
        if (post.Content.Length > AppConstants.ContentLimits.PostMaxLength)
            throw new ArgumentException($"본문이 허용 길이를 초과했습니다. (최대 {AppConstants.ContentLimits.PostMaxLength:N0}자)");

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

        // 리치 에디터 HTML — 저장 시 새니타이즈 (XSS 차단 단일 지점)
        post.Content = _contentSanitizer.Sanitize(post.Content);

        // 새니타이즈로 외부 이미지 등이 제거되어 본문이 비게 된 경우 차단
        // (UI의 빈 값 검사는 새니타이즈 전이라 이 케이스를 거르지 못함)
        if (HtmlContentHelper.IsEmptyHtml(post.Content))
            throw new ArgumentException("본문 내용이 비어 있습니다. (외부 이미지/임베드는 저장 시 제거됩니다)");

        post.IsHtml = true;
        post.SearchText = ContentSanitizer.ToPlainText(post.Content);
        post.CreatedAt = DateTime.UtcNow;

        context.Posts.Add(post);
        await context.SaveChangesAsync();

        // Id 확보 후, 본문에 삽입된 임시 미디어를 게시글 폴더로 이동하고 URL 치환
        var movedContent = await _fileUploadService.MoveContentTempMediaAsync(post.Content, post.Id);
        if (movedContent != post.Content)
        {
            post.Content = movedContent;
            await context.SaveChangesAsync();
        }

        return post;
    }

    public async Task<bool> UpdatePostAsync(int id, Post updatedPost, string? password = null)
    {
        if (updatedPost.Content.Length > AppConstants.ContentLimits.PostMaxLength)
            throw new ArgumentException($"본문이 허용 길이를 초과했습니다. (최대 {AppConstants.ContentLimits.PostMaxLength:N0}자)");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        if (!_authService.CanModifyContent(post.UserId, post.AuthorPassword, password))
            return false;

        // 저장 시 새니타이즈 + 임시 미디어 이동 (Id를 이미 알므로 단일 저장)
        var content = _contentSanitizer.Sanitize(updatedPost.Content);

        if (HtmlContentHelper.IsEmptyHtml(content))
            throw new ArgumentException("본문 내용이 비어 있습니다. (외부 이미지/임베드는 저장 시 제거됩니다)");

        content = await _fileUploadService.MoveContentTempMediaAsync(content, id);

        post.Title = updatedPost.Title;
        post.Content = content;
        post.IsHtml = true;
        post.SearchText = ContentSanitizer.ToPlainText(content);
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

        if (!_authService.CanModifyContent(post.UserId, post.AuthorPassword, password))
            return false;

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

            // 세션에 조회 기록 저장.
            // Blazor 서킷(SignalR) 안에서는 HTTP 응답이 이미 시작된 상태라 세션 쿠키가 없는
            // 사용자의 신규 세션을 확립할 수 없음 — 이 경우 중복 방지 기록만 생략하고
            // 조회수 증가 자체는 유지한다 (호출부에서 페이지 최초 로드 시 1회만 호출).
            try
            {
                httpContext.Session.SetString(viewedPostsKey, "1");
            }
            catch (InvalidOperationException)
            {
                // "The session cannot be established after the response has started"
            }
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
        var ipAddress = ClientIpHelper.GetClientIp(_httpContextAccessor.HttpContext);

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
        var ipAddress = ClientIpHelper.GetClientIp(_httpContextAccessor.HttpContext);

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

    public async Task<int> GetTotalPostCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts.CountAsync(p => !p.IsDeleted);
    }

    // 홈 화면은 익명에게도 노출되므로 완전 공개(IsPublic && !RequireAuth) 카테고리의 글만 집계
    public async Task<List<Post>> GetRecentPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive && p.Category.IsPublic && !p.Category.RequireAuth)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Post>> GetPopularPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive && p.Category.IsPublic && !p.Category.RequireAuth)
            .OrderByDescending(p => p.ViewCount)
            .ThenByDescending(p => p.VoteCount)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// 게시글 공지 고정/해제 (Admin, SubAdmin 권한 필요)
    /// </summary>
    public async Task<(bool Success, string Message)> TogglePinAsync(int postId)
    {
        if (!_authService.IsSubAdminOrHigher())
        {
            return (false, "공지 설정 권한이 없습니다.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(postId);
        if (post == null || post.IsDeleted)
        {
            return (false, "게시글을 찾을 수 없습니다.");
        }

        if (post.IsPinned)
        {
            // 고정 해제
            post.IsPinned = false;
            post.PinnedAt = null;
            await context.SaveChangesAsync();
            return (true, "공지가 해제되었습니다.");
        }
        else
        {
            // 고정 설정
            post.IsPinned = true;
            post.PinnedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return (true, "공지로 설정되었습니다.");
        }
    }

    /// <summary>
    /// 게시글이 공지인지 확인
    /// </summary>
    public async Task<bool> IsPinnedAsync(int postId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var post = await context.Posts.FindAsync(postId);
        return post?.IsPinned ?? false;
    }
}
