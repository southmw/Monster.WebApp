using System.ComponentModel.DataAnnotations;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public enum NotificationType
{
    CommentOnPost = 0,
    ReplyToComment = 1
}

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; } // 수신자

    public NotificationType Type { get; set; }

    public int PostId { get; set; }

    public int? CommentId { get; set; } // 알림을 발생시킨 댓글

    [StringLength(50)]
    public string ActorNickname { get; set; } = string.Empty; // 행위자 닉네임 스냅샷

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Post Post { get; set; } = null!;
    public Comment? Comment { get; set; }
}
