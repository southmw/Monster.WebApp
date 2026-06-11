using System.ComponentModel.DataAnnotations;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public class Comment
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public int? ParentCommentId { get; set; }

    public int? UserId { get; set; } // Null for anonymous comments

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// true면 Content가 저장 시 새니타이즈된 HTML(리치 에디터 작성),
    /// false면 레거시 평문 (출력 시 HtmlContentHelper.ToSafeHtml로 인코딩)
    /// </summary>
    public bool IsHtml { get; set; } = false;

    [Required]
    [StringLength(50)]
    public string AuthorNickname { get; set; } = string.Empty;

    [StringLength(255)]
    public string? AuthorPassword { get; set; } // Hashed password for anonymous comments

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public Post Post { get; set; } = null!;
    public User? User { get; set; }
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
