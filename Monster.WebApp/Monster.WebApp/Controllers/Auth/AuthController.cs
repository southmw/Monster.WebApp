using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monster.WebApp.Services.Auth;
using System.Security.Claims;

namespace Monster.WebApp.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _authService.LoginAsync(request.Username, request.Password);

        if (user == null)
        {
            return Unauthorized(new { message = "사용자명 또는 비밀번호가 올바르지 않습니다." });
        }

        return Ok(new { message = "로그인 성공", displayName = user.DisplayName });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return Ok(new { message = "로그아웃 성공" });
    }

    [HttpGet("logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await _authService.LogoutAsync();
        return Redirect("/");
    }

    // Debug endpoint - remove in production
    [HttpGet("check-user/{username}")]
    public async Task<IActionResult> CheckUser(string username, [FromServices] Monster.WebApp.Data.ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return NotFound(new { message = $"User '{username}' not found" });
        }

        return Ok(new {
            username = user.Username,
            email = user.Email,
            displayName = user.DisplayName,
            isActive = user.IsActive,
            hasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            passwordHashLength = user.PasswordHash?.Length ?? 0
        });
    }
}

public record LoginRequest(string Username, string Password);
