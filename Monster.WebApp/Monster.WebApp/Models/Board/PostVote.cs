using System.ComponentModel.DataAnnotations;
using Monster.WebApp.Models.Auth;

namespace Monster.WebApp.Models.Board;

public class PostVote
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public int? UserId { get; set; } // Null for anonymous votes

    [StringLength(45)]
    public string? IpAddress { get; set; } // For anonymous vote tracking

    public int VoteValue { get; set; } = 1; // +1 for upvote, -1 for downvote

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Post Post { get; set; } = null!;
    public User? User { get; set; }
}
