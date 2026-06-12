using Monster.WebApp.Models.Auth;
using Monster.WebApp.Models.Board;
using Monster.WebApp.Services.Board;

namespace Monster.WebApp.Tests;

/// <summary>
/// NotificationService — 댓글/답글 알림 생성 규칙, 읽음 처리 권한, 정리 정책 테스트.
/// HasData 시드: 카테고리 Id 1~3
/// </summary>
public class NotificationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    private readonly int _authorId;     // 게시글 작성자 (로그인)
    private readonly int _commenterId;  // 댓글 작성자 (로그인)
    private readonly int _postId;       // 로그인 사용자 게시글
    private readonly int _anonPostId;   // 익명 게시글

    public NotificationServiceTests()
    {
        using var context = _factory.CreateDbContext();

        var author = NewUser("author");
        var commenter = NewUser("commenter");
        context.Users.AddRange(author, commenter);
        context.SaveChanges();

        var post = NewPost(author.Id, "로그인 사용자 글");
        var anonPost = NewPost(null, "익명 글");
        context.Posts.AddRange(post, anonPost);
        context.SaveChanges();

        _authorId = author.Id;
        _commenterId = commenter.Id;
        _postId = post.Id;
        _anonPostId = anonPost.Id;
    }

    private static User NewUser(string name) => new()
    {
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "irrelevant",
        DisplayName = name,
        IsActive = true
    };

    private static Post NewPost(int? userId, string title) => new()
    {
        CategoryId = 1,
        UserId = userId,
        Title = title,
        Content = "<p>본문</p>",
        IsHtml = true,
        AuthorNickname = userId == null ? "익명" : $"user{userId}"
    };

    /// <summary>댓글을 DB에 저장하고 반환 (CreateForCommentAsync는 저장된 댓글을 전제)</summary>
    private Comment AddComment(int postId, int? userId, int? parentCommentId = null)
    {
        using var context = _factory.CreateDbContext();
        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            Content = "<p>댓글</p>",
            IsHtml = true,
            AuthorNickname = userId == null ? "익명" : $"user{userId}"
        };
        context.Comments.Add(comment);
        context.SaveChanges();
        return comment;
    }

    private NotificationService CreateService() => new(_factory);

    private List<Notification> AllNotifications()
    {
        using var context = _factory.CreateDbContext();
        return context.Notifications.ToList();
    }

    public void Dispose() => _factory.Dispose();

    // ---- 생성 규칙 ----

    [Fact]
    public async Task 타인이_댓글을_달면_글_작성자에게_알림이_생성된다()
    {
        var service = CreateService();
        var comment = AddComment(_postId, _commenterId);

        await service.CreateForCommentAsync(comment);

        var notification = Assert.Single(AllNotifications());
        Assert.Equal(_authorId, notification.UserId);
        Assert.Equal(NotificationType.CommentOnPost, notification.Type);
        Assert.Equal(_postId, notification.PostId);
        Assert.Equal(comment.Id, notification.CommentId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task 본인_글에_본인이_댓글을_달면_알림이_생성되지_않는다()
    {
        var service = CreateService();
        var comment = AddComment(_postId, _authorId);

        await service.CreateForCommentAsync(comment);

        Assert.Empty(AllNotifications());
    }

    [Fact]
    public async Task 익명_글에_댓글이_달리면_알림이_생성되지_않는다()
    {
        var service = CreateService();
        var comment = AddComment(_anonPostId, _commenterId);

        await service.CreateForCommentAsync(comment);

        Assert.Empty(AllNotifications());
    }

    [Fact]
    public async Task 답글은_부모_댓글_작성자에게_알림이_생성된다()
    {
        var service = CreateService();
        var parent = AddComment(_postId, _commenterId);
        var reply = AddComment(_postId, _authorId, parent.Id);

        await service.CreateForCommentAsync(reply);

        var notification = Assert.Single(AllNotifications());
        Assert.Equal(_commenterId, notification.UserId);
        Assert.Equal(NotificationType.ReplyToComment, notification.Type);
    }

    [Fact]
    public async Task 익명_부모_댓글에_답글은_알림이_생성되지_않는다()
    {
        var service = CreateService();
        var parent = AddComment(_postId, null);
        var reply = AddComment(_postId, _commenterId, parent.Id);

        await service.CreateForCommentAsync(reply);

        Assert.Empty(AllNotifications());
    }

    [Fact]
    public async Task 본인_댓글에_본인이_답글을_달면_알림이_생성되지_않는다()
    {
        var service = CreateService();
        var parent = AddComment(_postId, _commenterId);
        var reply = AddComment(_postId, _commenterId, parent.Id);

        await service.CreateForCommentAsync(reply);

        Assert.Empty(AllNotifications());
    }

    // ---- 읽음 처리 ----

    [Fact]
    public async Task 본인_알림은_읽음_처리할_수_있다()
    {
        var service = CreateService();
        var comment = AddComment(_postId, _commenterId);
        await service.CreateForCommentAsync(comment);
        var notification = Assert.Single(AllNotifications());

        Assert.True(await service.MarkAsReadAsync(notification.Id, _authorId));

        var updated = Assert.Single(AllNotifications());
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task 타인의_알림은_읽음_처리할_수_없다()
    {
        var service = CreateService();
        var comment = AddComment(_postId, _commenterId);
        await service.CreateForCommentAsync(comment);
        var notification = Assert.Single(AllNotifications());

        Assert.False(await service.MarkAsReadAsync(notification.Id, _commenterId));
        Assert.False(Assert.Single(AllNotifications()).IsRead);
    }

    [Fact]
    public async Task 전체_읽음_처리_후_미읽음_수는_0이_된다()
    {
        var service = CreateService();
        await service.CreateForCommentAsync(AddComment(_postId, _commenterId));
        var parent = AddComment(_postId, _commenterId);
        await service.CreateForCommentAsync(AddComment(_postId, _authorId, parent.Id));

        Assert.Equal(1, await service.GetUnreadCountAsync(_authorId));
        Assert.Equal(1, await service.GetUnreadCountAsync(_commenterId));

        await service.MarkAllAsReadAsync(_authorId);

        Assert.Equal(0, await service.GetUnreadCountAsync(_authorId));
        Assert.Equal(1, await service.GetUnreadCountAsync(_commenterId)); // 타인 알림은 영향 없음
    }

    // ---- 정리 정책 ----

    [Fact]
    public async Task 읽은지_30일_지난_알림은_새_알림_생성시_삭제된다()
    {
        var service = CreateService();

        var staleComment = AddComment(_postId, _commenterId);
        using (var context = _factory.CreateDbContext())
        {
            context.Notifications.Add(new Notification
            {
                UserId = _authorId,
                Type = NotificationType.CommentOnPost,
                PostId = _postId,
                CommentId = staleComment.Id,
                ActorNickname = "stale",
                IsRead = true,
                ReadAt = DateTime.UtcNow.AddDays(-31),
                CreatedAt = DateTime.UtcNow.AddDays(-40)
            });
            context.SaveChanges();
        }

        await service.CreateForCommentAsync(AddComment(_postId, _commenterId));

        var remaining = AllNotifications();
        var notification = Assert.Single(remaining);
        Assert.NotEqual("stale", notification.ActorNickname);
    }
}
