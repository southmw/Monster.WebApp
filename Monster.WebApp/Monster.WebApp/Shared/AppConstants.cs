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
}
