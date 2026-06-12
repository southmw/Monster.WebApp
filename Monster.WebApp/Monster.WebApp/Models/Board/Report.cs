using System.ComponentModel.DataAnnotations;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public enum ReportReason
{
    Spam = 0,
    Abuse = 1,
    Adult = 2,
    Privacy = 3,
    Etc = 4
}

public enum ReportStatus
{
    Pending = 0,
    Resolved = 1,
    Dismissed = 2
}

public class Report
{
    public int Id { get; set; }

    public int PostId { get; set; } // 댓글 신고여도 부모 게시글 Id 저장 (이동 링크용)

    public int? CommentId { get; set; } // null이면 게시글 신고

    public ReportReason Reason { get; set; }

    [StringLength(1000)]
    public string? Detail { get; set; }

    public int? ReporterUserId { get; set; } // Null for anonymous reports

    [StringLength(45)]
    public string? ReporterIp { get; set; } // For anonymous report tracking

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    // FK 미설정 (ReporterUserId와 함께 Users로 향하는 SET NULL FK가 2개면
    // SQL Server 다중 캐스케이드 경로 제약에 걸림) — 표시는 닉네임 스냅샷 사용
    public int? ResolvedByUserId { get; set; }

    [StringLength(50)]
    public string? ResolvedByNickname { get; set; }

    [StringLength(500)]
    public string? ResolutionNote { get; set; }

    // Navigation properties
    public Post Post { get; set; } = null!;
    public Comment? Comment { get; set; }
    public User? ReporterUser { get; set; }
}
