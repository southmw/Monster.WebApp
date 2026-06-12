using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Board;

public class CommentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;
    private readonly ContentSanitizer _contentSanitizer;
    private readonly FileUploadService _fileUploadService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<CommentService> _logger;

    public CommentService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuthService authService,
        ContentSanitizer contentSanitizer,
        FileUploadService fileUploadService,
        NotificationService notificationService,
        ILogger<CommentService> logger)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _contentSanitizer = contentSanitizer;
        _fileUploadService = fileUploadService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<List<Comment>> GetCommentsByPostIdAsync(int postId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Comments
            .AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment> CreateCommentAsync(Comment comment, string? password = null)
    {
        if (comment.Content.Length > AppConstants.ContentLimits.CommentMaxLength)
            throw new ArgumentException($"댓글이 허용 길이를 초과했습니다. (최대 {AppConstants.ContentLimits.CommentMaxLength:N0}자)");

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Set UserId if user is authenticated
        var userId = _authService.GetCurrentUserId();
        if (userId != null)
        {
            comment.UserId = userId;
            comment.AuthorPassword = null; // Authenticated users don't need password
        }
        else
        {
            // Anonymous comment requires password
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required for anonymous comments");

            comment.UserId = null;
            comment.AuthorPassword = BCrypt.Net.BCrypt.HashPassword(password);
        }

        // 리치 에디터 HTML — 저장 시 새니타이즈 (XSS 차단 단일 지점)
        comment.Content = _contentSanitizer.Sanitize(comment.Content);

        // 새니타이즈로 본문이 비게 된 경우 차단 (UI 검사는 새니타이즈 전)
        if (HtmlContentHelper.IsEmptyHtml(comment.Content))
            throw new ArgumentException("댓글 내용이 비어 있습니다. (외부 이미지/임베드는 저장 시 제거됩니다)");

        // 댓글에 삽입된 임시 이미지를 부모 게시글 폴더로 이동 (PostId는 호출 측에서 설정됨)
        comment.Content = await _fileUploadService.MoveContentTempMediaAsync(comment.Content, comment.PostId);

        comment.IsHtml = true;
        comment.CreatedAt = DateTime.UtcNow;

        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        // 알림 생성 실패가 댓글 등록을 깨지 않도록 격리
        try
        {
            await _notificationService.CreateForCommentAsync(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "댓글 알림 생성 실패 (CommentId: {CommentId})", comment.Id);
        }

        return comment;
    }

    /// <summary>
    /// 사용자가 작성한 댓글 목록 (프로필 활동 탭용). 삭제된 댓글과 삭제된 글의 댓글은 제외.
    /// </summary>
    public async Task<(List<Comment> Comments, int TotalCount)> GetCommentsByUserAsync(
        int userId, int page = 1, int pageSize = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Comments
            .AsNoTracking()
            .Where(c => c.UserId == userId && !c.IsDeleted && !c.Post.IsDeleted);

        var totalCount = await query.CountAsync();

        var comments = await query
            .Include(c => c.Post)
            .ThenInclude(p => p.Category)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (comments, totalCount);
    }

    public async Task<bool> UpdateCommentAsync(int id, string content, string? password = null)
    {
        if (content.Length > AppConstants.ContentLimits.CommentMaxLength)
            throw new ArgumentException($"댓글이 허용 길이를 초과했습니다. (최대 {AppConstants.ContentLimits.CommentMaxLength:N0}자)");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        if (!_authService.CanModifyContent(comment.UserId, comment.AuthorPassword, password))
            return false;

        var sanitized = _contentSanitizer.Sanitize(content);

        if (HtmlContentHelper.IsEmptyHtml(sanitized))
            throw new ArgumentException("댓글 내용이 비어 있습니다. (외부 이미지/임베드는 저장 시 제거됩니다)");

        // 수정 중 새로 삽입된 임시 이미지를 부모 게시글 폴더로 이동
        sanitized = await _fileUploadService.MoveContentTempMediaAsync(sanitized, comment.PostId);

        comment.Content = sanitized;
        comment.IsHtml = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCommentAsync(int id, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        if (!_authService.CanModifyContent(comment.UserId, comment.AuthorPassword, password))
            return false;

        comment.IsDeleted = true;
        await context.SaveChangesAsync();
        return true;
    }
}
