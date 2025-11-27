using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class CommentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;

    public CommentService(IDbContextFactory<ApplicationDbContext> contextFactory, AuthService authService)
    {
        _contextFactory = contextFactory;
        _authService = authService;
    }

    public async Task<List<Comment>> GetCommentsByPostIdAsync(int postId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Comments
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment> CreateCommentAsync(Comment comment, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

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

        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        return comment;
    }

    public async Task<bool> UpdateCommentAsync(int id, string content, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        // 관리자는 모든 댓글 수정 가능
        if (_authService.IsAdmin())
        {
            comment.Content = content;
            comment.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return true;
        }

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (comment.UserId != null)
        {
            // Authenticated comment - must be owner
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

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCommentAsync(int id, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.FindAsync(id);
        if (comment == null || comment.IsDeleted)
            return false;

        // 관리자는 모든 댓글 삭제 가능
        if (_authService.IsAdmin())
        {
            comment.IsDeleted = true;
            await context.SaveChangesAsync();
            return true;
        }

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (comment.UserId != null)
        {
            // Authenticated comment - must be owner
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
        await context.SaveChangesAsync();
        return true;
    }
}
