using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Security.Claims;
using Monster.WebApp.Components;
using Monster.WebApp.Data;
using Monster.WebApp.Services.Auth;
using Monster.WebApp.Services;
using Monster.WebApp.Services.Board;
using Monster.WebApp.Shared;
using MudBlazor.Services;
using Serilog;
using System.Diagnostics;

namespace Monster.WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                // 프레임워크 잡음 억제 + EF Core의 SQL/파라미터 로깅 차단(민감정보 노출 방지)
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    "logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31) // 최근 31일치만 보관 (디스크 누적 방지)
                .CreateLogger();

            builder.Host.UseSerilog();

            // Add DbContext with Factory for Blazor Server concurrency support
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            Log.Debug($"ConnectionString: {connectionString}");
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

                    // 쿠키 보안 강화
                    options.Cookie.HttpOnly = true; // JS 접근 차단 (XSS 시 쿠키 탈취 방어)
                    options.Cookie.SameSite = SameSiteMode.Lax; // CSRF 완화 (로그인 폼 호환 위해 Lax)
                    // 개발(http)에서는 SameAsRequest, 프로덕션(https)에서는 Always 강제
                    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;

                    // 쿠키의 사용자가 DB에 실재하고 활성 상태인지 검증.
                    // - 다른 DB에서 발급된 스테일 쿠키(개발 중 연결 전환, DB 재생성)가 FK 오류를 일으키는 것 방지
                    // - 비활성화/삭제된 사용자의 기존 세션을 무효화 (기존엔 쿠키 만료까지 7일간 유효했음)
                    // 매 요청 DB 조회를 피하기 위해 사용자별 5분 캐시 사용.
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!int.TryParse(userIdClaim, out var userId))
                        {
                            context.RejectPrincipal();
                            await TrySignOutAsync(context.HttpContext);
                            return;
                        }

                        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                        var cacheKey = $"user_valid_{userId}";

                        if (!cache.TryGetValue(cacheKey, out bool isValid))
                        {
                            var contextFactory = context.HttpContext.RequestServices
                                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                            await using var db = await contextFactory.CreateDbContextAsync();
                            isValid = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive);
                            cache.Set(cacheKey, isValid, TimeSpan.FromMinutes(5));
                        }

                        if (!isValid)
                        {
                            context.RejectPrincipal();
                            await TrySignOutAsync(context.HttpContext);
                        }
                    };
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
            // 익명 글 수정 비밀번호 전달용 (서킷 범위 — URL 노출 방지)
            builder.Services.AddScoped<PostEditVerificationState>();

            // 리치 에디터 HTML 새니타이저 (구성이 생성자에서 고정되므로 싱글톤으로 안전)
            builder.Services.AddSingleton<ContentSanitizer>();

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

            // Configure file upload size limits (에디터 이미지 10MB + 여유)
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 15_728_640; // 15MB
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 15_728_640; // 15MB
            });

            // Add controllers for API endpoints
            builder.Services.AddControllers();

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            var app = builder.Build();

            // 신뢰할 수 있는 프록시(ForwardedHeaders:KnownProxies 설정) 뒤에 배포된 경우에만
            // X-Forwarded-For/Proto를 수용해 RemoteIpAddress를 재작성.
            // 미설정 시 미들웨어를 등록하지 않으므로 클라이언트의 헤더 스푸핑이 IP 판정에 영향을 주지 않음.
            var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
            if (knownProxies is { Length: > 0 })
            {
                var forwardedOptions = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                };
                foreach (var proxy in knownProxies)
                {
                    if (IPAddress.TryParse(proxy, out var proxyIp))
                    {
                        forwardedOptions.KnownProxies.Add(proxyIp);
                    }
                    else
                    {
                        Log.Warning("ForwardedHeaders:KnownProxies에 잘못된 IP가 있습니다: {Proxy}", proxy);
                    }
                }
                app.UseForwardedHeaders(forwardedOptions);
            }

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

            // 응답 보안 헤더 (MIME 스니핑/클릭재킹 방어)
            // CSP는 Blazor Server + MudBlazor의 인라인 스크립트/스타일 의존성 때문에 별도 검토 후 도입
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                await next();
            });

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

            // 개발 환경에서는 시작 시 마이그레이션 자동 적용 (프로덕션은 배포 파이프라인에서 수행)
            await ApplyMigrationsAsync(app);

            // Initialize default admin account
            await InitializeDefaultAdminAsync(app);

            app.Run();
        }

        /// <summary>
        /// 스테일 쿠키 무효화용 로그아웃. WebSocket 업그레이드 요청 등 응답 헤더를 쓸 수 없는
        /// 상황에서는 Set-Cookie가 불가능해 예외가 날 수 있음 — 이때는 RejectPrincipal만으로 충분.
        /// </summary>
        private static async Task TrySignOutAsync(HttpContext httpContext)
        {
            try
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch (InvalidOperationException)
            {
                // 응답 시작 후에는 쿠키 삭제 불가 — 다음 일반 HTTP 요청에서 재시도됨
            }
        }

        private static async Task ApplyMigrationsAsync(WebApplication app)
        {
            // 개발 환경에서만 자동 적용. 프로덕션은 `dotnet ef database update`를 배포 단계에서 실행.
            if (!app.Environment.IsDevelopment())
                return;

            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                await context.Database.MigrateAsync();
                logger.LogInformation("데이터베이스 마이그레이션이 적용되었습니다.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "데이터베이스 마이그레이션 적용 실패");
            }
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
                var configuration = services.GetRequiredService<IConfiguration>();

                // 관리자 시드 정보는 설정(AdminSeed) 또는 환경변수에서 읽음 — 하드코딩 제거
                var adminUsername = configuration["AdminSeed:Username"] ?? "admin";
                var adminEmail = configuration["AdminSeed:Email"] ?? "admin@southmw.com";
                var adminPassword = configuration["AdminSeed:Password"];
                var usingFallbackPassword = string.IsNullOrWhiteSpace(adminPassword);
                if (usingFallbackPassword)
                {
                    // 설정 미지정 시 첫 부팅용 기본값 (프로덕션에서는 반드시 설정/변경 필요)
                    adminPassword = "Admin@123!";
                }

                // Check if admin user already exists
                var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
                if (adminUser == null)
                {
                    // Create default admin account
                    var newUser = await authService.RegisterAsync(
                        username: adminUsername,
                        email: adminEmail,
                        password: adminPassword!,
                        displayName: "관리자"
                    );

                    if (newUser != null)
                    {
                        // Get Admin role
                        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == AppConstants.Roles.Admin);
                        if (adminRole != null)
                        {
                            await roleService.AssignRoleAsync(newUser.Id, adminRole.Id);
                            // 비밀번호는 로그에 기록하지 않음
                            logger.LogInformation("기본 관리자 계정이 생성되었습니다. (Username: {Username})", adminUsername);
                            if (usingFallbackPassword)
                            {
                                logger.LogWarning("관리자 비밀번호가 설정(AdminSeed:Password)되지 않아 기본값으로 생성되었습니다. 프로덕션에서는 즉시 변경하세요.");
                            }
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
