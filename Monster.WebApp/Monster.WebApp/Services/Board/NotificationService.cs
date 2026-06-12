using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;

namespace Monster.WebApp.Services.Board;

public class NotificationService
{
    // 정리 정책: 읽은 지 30일 경과 삭제 + 사용자당 최대 보관 건수 초과분 삭제 (생성 시 기회적 수행)
    private const int ReadRetentionDays = 30;
    private const int MaxPerUser = 500;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 댓글 생성에 따른 알림 생성.
    /// 답글이면 부모 댓글 작성자에게, 일반 댓글이면 게시글 작성자에게 알림.
    /// 수신자가 익명 작성물(UserId null)이거나 본인 행동이면 생성하지 않음.
    /// </summary>
    public async Task CreateForCommentAsync(Comment comment)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        int? recipientUserId;
        NotificationType type;

        if (comment.ParentCommentId != null)
        {
            recipientUserId = await context.Comments
                .Where(c => c.Id == comment.ParentCommentId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
            type = NotificationType.ReplyToComment;
        }
        else
        {
            recipientUserId = await context.Posts
                .Where(p => p.Id == comment.PostId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();
            type = NotificationType.CommentOnPost;
        }

        if (recipientUserId == null || recipientUserId == comment.UserId)
            return;

        context.Notifications.Add(new Notification
        {
            UserId = recipientUserId.Value,
            Type = type,
            PostId = comment.PostId,
            CommentId = comment.Id,
            ActorNickname = comment.AuthorNickname,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        await CleanupAsync(context, recipientUserId.Value);
    }

    private static async Task CleanupAsync(ApplicationDbContext context, int userId)
    {
        var readCutoff = DateTime.UtcNow.AddDays(-ReadRetentionDays);
        await context.Notifications
            .Where(n => n.UserId == userId && n.IsRead && n.ReadAt < readCutoff)
            .ExecuteDeleteAsync();

        // 보관 상한 초과분(오래된 순) 삭제 — OrderBy+Skip은 ExecuteDelete로 직번역되지 않아 Id 목록 경유
        var overflowIds = await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(MaxPerUser)
            .Select(n => n.Id)
            .ToListAsync();

        if (overflowIds.Count > 0)
        {
            await context.Notifications
                .Where(n => overflowIds.Contains(n.Id))
                .ExecuteDeleteAsync();
        }
    }

    public async Task<List<Notification>> GetRecentAsync(int userId, int count = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Notifications
            .AsNoTracking()
            .Include(n => n.Post)
            .ThenInclude(p => p.Category)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<(List<Notification> Notifications, int TotalCount)> GetPagedAsync(int userId, int page = 1, int pageSize = 20)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync();

        var notifications = await query
            .Include(n => n.Post)
            .ThenInclude(p => p.Category)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (notifications, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <summary>
    /// 알림 읽음 처리. userId가 알림 수신자와 일치할 때만 처리 (타인 알림 조작 차단).
    /// </summary>
    public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification == null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        return true;
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }
}
