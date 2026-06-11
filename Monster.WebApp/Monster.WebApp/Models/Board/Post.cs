using System.ComponentModel.DataAnnotations;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public class Post
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public int? UserId { get; set; } // Null for anonymous posts

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// true면 Content가 저장 시 새니타이즈된 HTML(리치 에디터 작성),
    /// false면 레거시 평문 (출력 시 HtmlContentHelper.ToSafeHtml로 인코딩)
    /// </summary>
    public bool IsHtml { get; set; } = false;

    /// <summary>HTML 태그를 제거한 본문 — 검색용 (IsHtml=true인 글에서 태그가 검색에 걸리지 않도록)</summary>
    public string? SearchText { get; set; }

    [Required]
    [StringLength(50)]
    public string AuthorNickname { get; set; } = string.Empty;

    [StringLength(255)]
    public string? AuthorPassword { get; set; } // Hashed password for anonymous posts

    public int ViewCount { get; set; } = 0;

    public int VoteCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public bool IsPinned { get; set; } = false;

    public DateTime? PinnedAt { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
