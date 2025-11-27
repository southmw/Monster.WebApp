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
