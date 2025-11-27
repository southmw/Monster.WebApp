using Microsoft.AspNetCore.Mvc;
using Monster.WebApp.Services.Auth;

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
        var (user, errorMessage) = await _authService.LoginAsync(request.Username, request.Password);

        if (user == null)
        {
            return Unauthorized(new { message = errorMessage ?? "사용자명 또는 비밀번호가 올바르지 않습니다." });
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
}

public record LoginRequest(string Username, string Password);
