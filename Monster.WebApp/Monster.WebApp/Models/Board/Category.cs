using System.ComponentModel.DataAnnotations;

namespace Monster.WebApp.Models.Board;

public class Category
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UrlSlug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPublic { get; set; } = true; // Public board accessible to all
    public bool RequireAuth { get; set; } = false; // Requires login to access

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<CategoryAccess> CategoryAccesses { get; set; } = new List<CategoryAccess>();
}
