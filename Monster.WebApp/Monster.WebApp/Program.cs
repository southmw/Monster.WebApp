using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Monster.WebApp.Components;
using Monster.WebApp.Data;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;
using MudBlazor.Services;
using Serilog;

namespace Monster.WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();

            // Add DbContext with Factory for Blazor Server concurrency support
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            // Also register DbContext for backward compatibility
            builder.Services.AddScoped<ApplicationDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            // Add HttpContextAccessor
            builder.Services.AddHttpContextAccessor();

            // Add HttpClient for server-side components
            builder.Services.AddScoped(sp =>
            {
                var navigationManager = sp.GetRequiredService<NavigationManager>();
                return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
            });

            // Add Authentication & Authorization
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/account/login";
                    options.LogoutPath = "/account/logout";
                    options.AccessDeniedPath = "/account/access-denied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.SlidingExpiration = true;
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(AppConstants.Policies.AdminOnly, policy => policy.RequireRole(AppConstants.Roles.Admin));
                options.AddPolicy(AppConstants.Policies.SubAdminOrHigher, policy => policy.RequireRole(AppConstants.Roles.Admin, AppConstants.Roles.SubAdmin));
                options.AddPolicy(AppConstants.Policies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
            });

            // Add application services
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<RoleService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<CategoryAccessService>();
            builder.Services.AddScoped<CategoryService>();
            builder.Services.AddScoped<PostService>();
            builder.Services.AddScoped<CommentService>();
            builder.Services.AddScoped<FileUploadService>();

            builder.Services.AddMudServices();

            // Add MemoryCache for login attempt limiting
            builder.Services.AddMemoryCache();

            // Add Session for view count tracking
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Configure file upload size limits
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52428800; // 50MB
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 52428800; // 50MB
            });

            // Add controllers for API endpoints
            builder.Services.AddControllers();

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found");
            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.MapControllers();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            // Initialize default admin account
            await InitializeDefaultAdminAsync(app);

            app.Run();
        }

        private static async Task InitializeDefaultAdminAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var authService = services.GetRequiredService<AuthService>();
                var roleService = services.GetRequiredService<RoleService>();

                // Check if admin user already exists
                var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                if (adminUser == null)
                {
                    // Create default admin account
                    var newUser = await authService.RegisterAsync(
                        username: "admin",
                        email: "admin@southmw.com",
                        password: "Admin@123!",
                        displayName: "관리자"
                    );

                    if (newUser != null)
                    {
                        // Get Admin role
                        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == AppConstants.Roles.Admin);
                        if (adminRole != null)
                        {
                            await roleService.AssignRoleAsync(newUser.Id, adminRole.Id);
                            logger.LogInformation("기본 관리자 계정이 생성되었습니다. (Username: admin, Password: Admin@123!)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "관리자 계정 초기화 실패");
            }
        }
    }
}
