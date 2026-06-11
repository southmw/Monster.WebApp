namespace Monster.WebApp.Shared;

public static class AppConstants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string SubAdmin = "SubAdmin";
        public const string User = "User";
    }

    public static class Policies
    {
        public const string AdminOnly = "AdminOnly";
        public const string SubAdminOrHigher = "SubAdminOrHigher";
        public const string AuthenticatedUser = "AuthenticatedUser";
    }

    /// <summary>본문 길이 상한 (새니타이즈 전 HTML 기준 — HTML 팽창·과대 입력 방어)</summary>
    public static class ContentLimits
    {
        public const int PostMaxLength = 100_000;
        public const int CommentMaxLength = 20_000;
    }

    /// <summary>MemoryCache 키 (여러 위치에서 공유하는 키만 정의)</summary>
    public static class CacheKeys
    {
        /// <summary>쿠키 사용자 유효성 캐시 — OnValidatePrincipal(Program.cs)/AuthService/UserService 공유</summary>
        public const string UserValidPrefix = "user_valid_";
    }
}
