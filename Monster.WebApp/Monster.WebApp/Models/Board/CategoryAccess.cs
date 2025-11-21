using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public enum AccessType
{
    Read = 1,
    Write = 2,
    Manage = 3
}

public class CategoryAccess
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Either UserId or RoleId should be set, not both
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? RoleId { get; set; }
    public Role? Role { get; set; }

    public AccessType AccessType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
