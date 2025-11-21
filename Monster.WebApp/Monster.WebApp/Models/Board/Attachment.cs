using System.ComponentModel.DataAnnotations;

namespace Monster.WebApp.Models.Board;

public class Attachment
{
    public int Id { get; set; }

    public int PostId { get; set; }

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Post Post { get; set; } = null!;
}
