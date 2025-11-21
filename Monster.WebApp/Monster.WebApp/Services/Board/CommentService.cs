using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class CommentService
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public CommentService(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<List<Comment>> GetCommentsByPostIdAsync(int postId)
    {
        return await _context.Comments
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment> CreateCommentAsync(Comment comment, string? password = null)
    {
        // Set UserId if user is authenticated
        var userId = _authService.GetCurrentUserId();
        if (userId != null)
        {
            comment.UserId = userId;
            comment.AuthorPassword = null; // Authenticated users don't need password
        }
        else
        {
            // Anonymous comment requires password
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required for anonymous comments");

            comment.UserId = null;
            comment.AuthorPassword = BCrypt.Net.BCrypt.HashPassword(password);
        }

        comment.CreatedAt = DateTime.UtcNow;

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        return comment;
    }

    public async Task<bool> UpdateCommentAsync(int id, string content, string? password = null)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (comment.UserId != null)
        {
            // Authenticated comment - must be owner or admin
            if (userId != comment.UserId)
                return false;
        }
        else
        {
            // Anonymous comment - verify password
            if (string.IsNullOrWhiteSpace(password) || comment.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, comment.AuthorPassword))
                return false;
        }

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCommentAsync(int id, string? password = null)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (comment.UserId != null)
        {
            // Authenticated comment - must be owner or admin
            if (userId != comment.UserId)
                return false;
        }
        else
        {
            // Anonymous comment - verify password
            if (string.IsNullOrWhiteSpace(password) || comment.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, comment.AuthorPassword))
                return false;
        }

        comment.IsDeleted = true;
        await _context.SaveChangesAsync();
        return true;
    }
}
