using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Data;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Auth;

namespace Monster.WebApp.Services.Board;

public class PostService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthService _authService;

    public PostService(IDbContextFactory<ApplicationDbContext> contextFactory, AuthService authService)
    {
        _contextFactory = contextFactory;
        _authService = authService;
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetPostsByCategoryAsync(
        int categoryId,
        int page = 1,
        int pageSize = 20,
        string? searchQuery = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Posts
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
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Post> CreatePostAsync(Post post, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

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

        context.Posts.Add(post);
        await context.SaveChangesAsync();

        return post;
    }

    public async Task<bool> UpdatePostAsync(int id, Post updatedPost, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
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

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePostAsync(int id, string? password = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted)
            return false;

        // 관리자는 모든 게시글 삭제 가능
        if (_authService.IsAdmin())
        {
            post.IsDeleted = true;
            await context.SaveChangesAsync();
            return true;
        }

        // Check authorization
        var userId = _authService.GetCurrentUserId();
        if (post.UserId != null)
        {
            // Authenticated post - must be owner
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
        await context.SaveChangesAsync();
        return true;
    }

    public async Task IncrementViewCountAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post != null && !post.IsDeleted)
        {
            post.ViewCount++;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> VotePostAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(id);
        if (post != null && !post.IsDeleted)
        {
            post.VoteCount++;
            await context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<int> GetTotalPostCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts.CountAsync(p => !p.IsDeleted);
    }

    public async Task<bool> UpdatePostContentAsync(int postId, string content)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var post = await context.Posts.FindAsync(postId);
        if (post == null || post.IsDeleted)
            return false;

        post.Content = content;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Post>> GetRecentPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Post>> GetPopularPostsAsync(int count = 5)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Posts
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.Category.IsActive)
            .OrderByDescending(p => p.ViewCount)
            .ThenByDescending(p => p.VoteCount)
            .Take(count)
            .ToListAsync();
    }
}
