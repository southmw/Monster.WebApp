using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class PostService
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public PostService(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetPostsByCategoryAsync(
        int categoryId,
        int page = 1,
        int pageSize = 20,
        string? searchQuery = null)
    {
        var query = _context.Posts
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(p => p.Title.Contains(searchQuery) || p.Content.Contains(searchQuery));
        }

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (posts, totalCount);
    }

    public async Task<Post?> GetPostByIdAsync(int id)
    {
        return await _context.Posts
            .Include(p => p.Category)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Post> CreatePostAsync(Post post, string? password = null)
    {
        // Set UserId if user is authenticated
        var userId = _authService.GetCurrentUserId();
        if (userId != null)
        {
            post.UserId = userId;
            post.AuthorPassword = null; // Authenticated users don't need password
        }
        else
        {
            // Anonymous post requires password
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required for anonymous posts");

            post.UserId = null;
            post.AuthorPassword = BCrypt.Net.BCrypt.HashPassword(password);
        }

        post.CreatedAt = DateTime.UtcNow;

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return post;
    }

    public async Task<bool> UpdatePostAsync(int id, Post updatedPost, string? password = null)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (post.UserId != null)
        {
            // Authenticated post - must be owner or admin
            if (userId != post.UserId)
                return false;
        }
        else
        {
            // Anonymous post - verify password
            if (string.IsNullOrWhiteSpace(password) || post.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, post.AuthorPassword))
                return false;
        }

        post.Title = updatedPost.Title;
        post.Content = updatedPost.Content;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePostAsync(int id, string? password = null)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (post.UserId != null)
        {
            // Authenticated post - must be owner or admin
            if (userId != post.UserId)
                return false;
        }
        else
        {
            // Anonymous post - verify password
            if (string.IsNullOrWhiteSpace(password) || post.AuthorPassword == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(password, post.AuthorPassword))
                return false;
        }

        post.IsDeleted = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task IncrementViewCountAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post != null && !post.IsDeleted)
        {
            post.ViewCount++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> VotePostAsync(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post != null && !post.IsDeleted)
        {
            post.VoteCount++;
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<int> GetTotalPostCountAsync()
    {
        return await _context.Posts.CountAsync(p => !p.IsDeleted);
    }
}
