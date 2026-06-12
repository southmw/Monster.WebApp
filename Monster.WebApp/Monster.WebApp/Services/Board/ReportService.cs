using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Services.Board;

public class ReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReportService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuthService authService,
        IHttpContextAccessor httpContextAccessor)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 게시글/댓글 신고 생성. commentId가 null이면 게시글 신고.
    /// 신고자는 로그인=UserId, 익명=IP로 기록하며 동일 신고자의 동일 대상 중복 신고를 차단.
    /// </summary>
    public async Task<(bool Success, string Message)> CreateReportAsync(
        int postId, int? commentId, ReportReason reason, string? detail)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var postExists = await context.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted);
        if (!postExists)
            return (false, "신고 대상 게시글을 찾을 수 없습니다.");

        if (commentId != null)
        {
            var commentExists = await context.Comments
                .AnyAsync(c => c.Id == commentId && c.PostId == postId && !c.IsDeleted);
            if (!commentExists)
                return (false, "신고 대상 댓글을 찾을 수 없습니다.");
        }

        var userId = _authService.GetCurrentUserId();
        var ipAddress = ClientIpHelper.GetClientIp(_httpContextAccessor.HttpContext);

        if (userId == null && string.IsNullOrEmpty(ipAddress))
            return (false, "신고자를 확인할 수 없습니다.");

        // 중복 신고 체크 (VotePostAsync와 동일한 서비스 레벨 검사)
        bool alreadyReported;
        if (userId != null)
        {
            alreadyReported = await context.Reports
                .AnyAsync(r => r.PostId == postId && r.CommentId == commentId && r.ReporterUserId == userId);
        }
        else
        {
            alreadyReported = await context.Reports
                .AnyAsync(r => r.PostId == postId && r.CommentId == commentId
                            && r.ReporterIp == ipAddress && r.ReporterUserId == null);
        }

        if (alreadyReported)
            return (false, "이미 신고한 게시물입니다.");

        if (detail?.Length > 1000)
            detail = detail[..1000];

        context.Reports.Add(new Report
        {
            PostId = postId,
            CommentId = commentId,
            Reason = reason,
            Detail = detail,
            ReporterUserId = userId,
            ReporterIp = userId == null ? ipAddress : null,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return (true, "신고가 접수되었습니다.");
    }

    public async Task<(List<Report> Reports, int TotalCount)> GetReportsAsync(
        ReportStatus? status = null, int page = 1, int pageSize = 20)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Reports.AsNoTracking().AsQueryable();
        if (status != null)
            query = query.Where(r => r.Status == status);

        var totalCount = await query.CountAsync();

        var reports = await query
            .Include(r => r.Post)
            .ThenInclude(p => p.Category)
            .Include(r => r.Comment)
            .Include(r => r.ReporterUser)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (reports, totalCount);
    }

    public async Task<int> GetPendingCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Reports.CountAsync(r => r.Status == ReportStatus.Pending);
    }

    /// <summary>
    /// 신고 처리 (SubAdmin 이상). deleteContent=true면 대상 콘텐츠를 soft-delete하고
    /// 동일 대상의 다른 대기 신고도 일괄 처리. 콘텐츠 삭제는 CanModifyContent 경로가 아닌
    /// 별도 운영 행위(공지 고정과 동급)로, 서비스 레벨에서 권한을 재검증한다.
    /// </summary>
    public async Task<(bool Success, string Message)> ResolveAsync(int reportId, bool deleteContent, string? note = null)
    {
        if (!_authService.IsSubAdminOrHigher())
            return (false, "신고 처리 권한이 없습니다.");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var report = await context.Reports.FindAsync(reportId);
        if (report == null)
            return (false, "신고를 찾을 수 없습니다.");
        if (report.Status != ReportStatus.Pending)
            return (false, "이미 처리된 신고입니다.");

        var resolverId = _authService.GetCurrentUserId();
        var resolverNickname = _authService.GetCurrentUserDisplayName();
        var now = DateTime.UtcNow;

        ApplyResolution(report, ReportStatus.Resolved, resolverId, resolverNickname, note, now);

        if (deleteContent)
        {
            if (report.CommentId != null)
            {
                var comment = await context.Comments.FindAsync(report.CommentId);
                if (comment != null)
                    comment.IsDeleted = true;
            }
            else
            {
                var post = await context.Posts.FindAsync(report.PostId);
                if (post != null)
                    post.IsDeleted = true;
            }

            // 동일 대상의 다른 대기 신고 일괄 처리
            var siblings = await context.Reports
                .Where(r => r.Id != report.Id
                         && r.PostId == report.PostId
                         && r.CommentId == report.CommentId
                         && r.Status == ReportStatus.Pending)
                .ToListAsync();
            foreach (var sibling in siblings)
                ApplyResolution(sibling, ReportStatus.Resolved, resolverId, resolverNickname, "동일 대상 일괄 처리", now);
        }

        await context.SaveChangesAsync();
        return (true, deleteContent ? "신고가 처리되고 대상 콘텐츠가 삭제되었습니다." : "신고가 처리되었습니다.");
    }

    /// <summary>
    /// 신고 기각 (SubAdmin 이상). 대상 콘텐츠는 유지.
    /// </summary>
    public async Task<(bool Success, string Message)> DismissAsync(int reportId, string? note = null)
    {
        if (!_authService.IsSubAdminOrHigher())
            return (false, "신고 처리 권한이 없습니다.");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var report = await context.Reports.FindAsync(reportId);
        if (report == null)
            return (false, "신고를 찾을 수 없습니다.");
        if (report.Status != ReportStatus.Pending)
            return (false, "이미 처리된 신고입니다.");

        ApplyResolution(report, ReportStatus.Dismissed,
            _authService.GetCurrentUserId(), _authService.GetCurrentUserDisplayName(), note, DateTime.UtcNow);

        await context.SaveChangesAsync();
        return (true, "신고가 기각되었습니다.");
    }

    private static void ApplyResolution(
        Report report, ReportStatus status, int? resolverId, string? resolverNickname, string? note, DateTime now)
    {
        report.Status = status;
        report.ResolvedAt = now;
        report.ResolvedByUserId = resolverId;
        report.ResolvedByNickname = resolverNickname;
        report.ResolutionNote = note?.Length > 500 ? note[..500] : note;
    }
}
