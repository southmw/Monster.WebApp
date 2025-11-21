using Monster.WebApp.Models.Board;

namespace Monster.WebApp.Models.Auth;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName => Description; // Computed property for backward compatibility
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<CategoryAccess> CategoryAccesses { get; set; } = new List<CategoryAccess>();
}
