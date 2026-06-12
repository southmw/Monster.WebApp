using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// ReportService — 신고 생성(로그인/익명, 중복 방지), 처리/기각 권한 및 soft-delete 테스트.
/// HasData 시드: 역할 Id 1=Admin, 2=SubAdmin, 3=User / 카테고리 Id 1~3
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private readonly int _reporterId;   // 일반 사용자 (신고자)
    private readonly int _subAdminId;   // SubAdmin (처리자)
    private readonly int _postId;
    private readonly int _commentId;

    public ReportServiceTests()
    {
        using var context = _factory.CreateDbContext();

        var reporter = NewUser("reporter");
        var subAdmin = NewUser("subadmin");
        context.Users.AddRange(reporter, subAdmin);
        context.SaveChanges();

        var post = new Post
        {
            CategoryId = 1,
            Title = "신고 대상 글",
            Content = "<p>본문</p>",
            IsHtml = true,
            AuthorNickname = "작성자"
        };
        context.Posts.Add(post);
        context.SaveChanges();

        var comment = new Comment
        {
            PostId = post.Id,
            Content = "<p>댓글</p>",
            IsHtml = true,
            AuthorNickname = "댓글러"
        };
        context.Comments.Add(comment);
        context.SaveChanges();

        _reporterId = reporter.Id;
        _subAdminId = subAdmin.Id;
        _postId = post.Id;
        _commentId = comment.Id;
    }

    private static User NewUser(string name) => new()
    {
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "irrelevant",
        DisplayName = name,
        IsActive = true
    };

    private ReportService CreateService(IHttpContextAccessor accessor)
        => new(_factory, new AuthService(_factory, accessor, _cache), accessor);

    private ReportService AsUser(int userId, params string[] roles)
        => CreateService(TestHttpContext.AuthenticatedAs(userId, roles));

    private ReportService AsAnonymous(string ip = "10.0.0.1")
    {
        var accessor = TestHttpContext.Anonymous();
        accessor.HttpContext!.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return CreateService(accessor);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _cache.Dispose();
    }

    // ---- 신고 생성 ----

    [Fact]
    public async Task 로그인_사용자가_게시글을_신고할_수_있다()
    {
        var service = AsUser(_reporterId);

        var (success, _) = await service.CreateReportAsync(_postId, null, ReportReason.Spam, "광고 글");

        Assert.True(success);
        using var context = _factory.CreateDbContext();
        var report = Assert.Single(context.Reports.ToList());
        Assert.Equal(_reporterId, report.ReporterUserId);
        Assert.Null(report.ReporterIp);
        Assert.Equal(ReportStatus.Pending, report.Status);
    }

    [Fact]
    public async Task 익명_사용자는_IP로_신고가_기록된다()
    {
        var service = AsAnonymous("10.0.0.7");

        var (success, _) = await service.CreateReportAsync(_postId, _commentId, ReportReason.Abuse, null);

        Assert.True(success);
        using var context = _factory.CreateDbContext();
        var report = Assert.Single(context.Reports.ToList());
        Assert.Null(report.ReporterUserId);
        Assert.Equal("10.0.0.7", report.ReporterIp);
        Assert.Equal(_commentId, report.CommentId);
    }

    [Fact]
    public async Task 동일_사용자가_같은_대상을_중복_신고할_수_없다()
    {
        var service = AsUser(_reporterId);
        await service.CreateReportAsync(_postId, null, ReportReason.Spam, null);

        var (success, _) = await service.CreateReportAsync(_postId, null, ReportReason.Abuse, null);

        Assert.False(success);
    }

    [Fact]
    public async Task 동일_IP의_익명_중복_신고는_차단되고_다른_IP는_허용된다()
    {
        await AsAnonymous("10.0.0.1").CreateReportAsync(_postId, null, ReportReason.Spam, null);

        var (duplicate, _) = await AsAnonymous("10.0.0.1").CreateReportAsync(_postId, null, ReportReason.Spam, null);
        var (other, _) = await AsAnonymous("10.0.0.2").CreateReportAsync(_postId, null, ReportReason.Spam, null);

        Assert.False(duplicate);
        Assert.True(other);
    }

    [Fact]
    public async Task 같은_게시글_신고와_댓글_신고는_별개_대상으로_허용된다()
    {
        var service = AsUser(_reporterId);
        await service.CreateReportAsync(_postId, null, ReportReason.Spam, null);

        var (success, _) = await service.CreateReportAsync(_postId, _commentId, ReportReason.Spam, null);

        Assert.True(success);
    }

    [Fact]
    public async Task 삭제된_게시글은_신고할_수_없다()
    {
        using (var context = _factory.CreateDbContext())
        {
            context.Posts.Find(_postId)!.IsDeleted = true;
            context.SaveChanges();
        }

        var (success, _) = await AsUser(_reporterId).CreateReportAsync(_postId, null, ReportReason.Spam, null);

        Assert.False(success);
    }

    // ---- 처리/기각 ----

    [Fact]
    public async Task SubAdmin이_콘텐츠_삭제와_함께_처리하면_대상이_soft_delete되고_동일_대상_대기_신고가_일괄_처리된다()
    {
        await AsAnonymous("10.0.0.1").CreateReportAsync(_postId, null, ReportReason.Spam, null);
        await AsAnonymous("10.0.0.2").CreateReportAsync(_postId, null, ReportReason.Abuse, null);
        int firstReportId;
        using (var context = _factory.CreateDbContext())
        {
            firstReportId = context.Reports.First().Id;
        }

        var service = AsUser(_subAdminId, AppConstants.Roles.SubAdmin);
        var (success, _) = await service.ResolveAsync(firstReportId, deleteContent: true, "스팸 확인");

        Assert.True(success);
        using var verify = _factory.CreateDbContext();
        Assert.True(verify.Posts.Find(_postId)!.IsDeleted);
        Assert.All(verify.Reports.ToList(), r => Assert.Equal(ReportStatus.Resolved, r.Status));
    }

    [Fact]
    public async Task 댓글_신고를_콘텐츠_삭제와_함께_처리하면_댓글만_soft_delete된다()
    {
        await AsUser(_reporterId).CreateReportAsync(_postId, _commentId, ReportReason.Abuse, null);
        int reportId;
        using (var context = _factory.CreateDbContext())
        {
            reportId = context.Reports.First().Id;
        }

        var service = AsUser(_subAdminId, AppConstants.Roles.SubAdmin);
        var (success, _) = await service.ResolveAsync(reportId, deleteContent: true, null);

        Assert.True(success);
        using var verify = _factory.CreateDbContext();
        Assert.True(verify.Comments.Find(_commentId)!.IsDeleted);
        Assert.False(verify.Posts.Find(_postId)!.IsDeleted);
    }

    [Fact]
    public async Task 일반_사용자는_신고를_처리할_수_없다()
    {
        await AsUser(_reporterId).CreateReportAsync(_postId, null, ReportReason.Spam, null);
        int reportId;
        using (var context = _factory.CreateDbContext())
        {
            reportId = context.Reports.First().Id;
        }

        var (resolve, _) = await AsUser(_reporterId, AppConstants.Roles.User).ResolveAsync(reportId, true, null);
        var (dismiss, _) = await AsUser(_reporterId, AppConstants.Roles.User).DismissAsync(reportId, null);

        Assert.False(resolve);
        Assert.False(dismiss);
        using var verify = _factory.CreateDbContext();
        Assert.Equal(ReportStatus.Pending, verify.Reports.Find(reportId)!.Status);
    }

    [Fact]
    public async Task 기각하면_대상_콘텐츠는_유지된다()
    {
        await AsUser(_reporterId).CreateReportAsync(_postId, null, ReportReason.Spam, null);
        int reportId;
        using (var context = _factory.CreateDbContext())
        {
            reportId = context.Reports.First().Id;
        }

        var service = AsUser(_subAdminId, AppConstants.Roles.SubAdmin);
        var (success, _) = await service.DismissAsync(reportId, "문제 없음");

        Assert.True(success);
        using var verify = _factory.CreateDbContext();
        Assert.Equal(ReportStatus.Dismissed, verify.Reports.Find(reportId)!.Status);
        Assert.False(verify.Posts.Find(_postId)!.IsDeleted);
    }

    [Fact]
    public async Task 이미_처리된_신고는_다시_처리할_수_없다()
    {
        await AsUser(_reporterId).CreateReportAsync(_postId, null, ReportReason.Spam, null);
        int reportId;
        using (var context = _factory.CreateDbContext())
        {
            reportId = context.Reports.First().Id;
        }

        var service = AsUser(_subAdminId, AppConstants.Roles.SubAdmin);
        await service.DismissAsync(reportId, null);

        var (success, _) = await service.ResolveAsync(reportId, true, null);
        Assert.False(success);
    }

    // ---- 조회 ----

    [Fact]
    public async Task 상태_필터로_신고_목록을_조회할_수_있다()
    {
        await AsAnonymous("10.0.0.1").CreateReportAsync(_postId, null, ReportReason.Spam, null);
        await AsAnonymous("10.0.0.2").CreateReportAsync(_postId, _commentId, ReportReason.Abuse, null);

        var service = AsUser(_subAdminId, AppConstants.Roles.SubAdmin);
        int dismissTargetId;
        using (var context = _factory.CreateDbContext())
        {
            dismissTargetId = context.Reports.First().Id;
        }
        await service.DismissAsync(dismissTargetId, null);

        var (pending, pendingCount) = await service.GetReportsAsync(ReportStatus.Pending);
        var (all, allCount) = await service.GetReportsAsync(null);

        Assert.Equal(1, pendingCount);
        Assert.Single(pending);
        Assert.Equal(2, allCount);
        Assert.Equal(2, all.Count);
        Assert.Equal(1, await service.GetPendingCountAsync());
    }
}
