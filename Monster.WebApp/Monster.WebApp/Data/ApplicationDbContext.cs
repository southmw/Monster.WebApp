using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Shared;

namespace Monster.WebApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<CategoryAccess> CategoryAccesses { get; set; }
    public DbSet<PostVote> PostVotes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Role configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // UserRole configuration (many-to-many)
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CategoryAccess configuration
        modelBuilder.Entity<CategoryAccess>(entity =>
        {
            entity.HasOne(ca => ca.Category)
                .WithMany(c => c.CategoryAccesses)
                .HasForeignKey(ca => ca.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ca => ca.User)
                .WithMany(u => u.CategoryAccesses)
                .HasForeignKey(ca => ca.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ca => ca.Role)
                .WithMany(r => r.CategoryAccesses)
                .HasForeignKey(ca => ca.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RoleId);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.UrlSlug).IsUnique();
            entity.HasIndex(e => e.DisplayOrder);
        });

        // Post configuration
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasOne(p => p.Category)
                .WithMany(c => c.Posts)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Comment configuration
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.PostId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ParentCommentId);
        });

        // Attachment configuration
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasOne(a => a.Post)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PostId);
        });

        // PostVote configuration
        modelBuilder.Entity<PostVote>(entity =>
        {
            entity.HasOne(pv => pv.Post)
                .WithMany()
                .HasForeignKey(pv => pv.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pv => pv.User)
                .WithMany()
                .HasForeignKey(pv => pv.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Unique index: one vote per user per post
            entity.HasIndex(e => new { e.PostId, e.UserId })
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            // Index for IP-based vote tracking (anonymous users)
            entity.HasIndex(e => new { e.PostId, e.IpAddress });
        });

        // Seed initial roles
        var seedDate = new DateTime(2025, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 1,
                Name = AppConstants.Roles.Admin,
                Description = "전체 관리자 - 모든 권한",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = 2,
                Name = AppConstants.Roles.SubAdmin,
                Description = "서브 관리자 - 제한된 관리 권한",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = 3,
                Name = AppConstants.Roles.User,
                Description = "일반 사용자",
                CreatedAt = seedDate
            }
        );

        // Note: Test users can be created through registration page
        // Default password for manual testing: use any password you prefer

        // Seed initial categories
        modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Id = 1,
                Name = "자유게시판",
                UrlSlug = "free",
                Description = "자유롭게 의견을 나누는 공간입니다.",
                DisplayOrder = 1,
                IsActive = true,
                IsPublic = true,
                RequireAuth = false,
                CreatedAt = seedDate
            },
            new Category
            {
                Id = 2,
                Name = "질문게시판",
                UrlSlug = "questions",
                Description = "궁금한 점을 질문하고 답변을 받는 공간입니다.",
                DisplayOrder = 2,
                IsActive = true,
                IsPublic = true,
                RequireAuth = false,
                CreatedAt = seedDate
            },
            new Category
            {
                Id = 3,
                Name = "정보공유",
                UrlSlug = "info",
                Description = "유용한 정보를 공유하는 공간입니다.",
                DisplayOrder = 3,
                IsActive = true,
                IsPublic = true,
                RequireAuth = false,
                CreatedAt = seedDate
            }
        );
    }
}
