using JobHunt.Api.Models.Requests.Auth;
using JobHunt.Api.Models.Responses.Auth;
using JobHunt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using JobHunt.Api.Entities;

namespace JobHunt.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(AuthService authService, AuthSessionService sessions,
    UserManager<ApplicationUser> users, CurrentUser currentUser, IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    private const string CookieName = "jobhunt.refresh";
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserResponse>> Register(RegisterUserRequest request)
    {
        var result = await authService.RegisterAsync(request);
        foreach (var error in result.ValidationErrors)
            foreach (var message in error.Value) ModelState.AddModelError(error.Key, message);

        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return StatusCode(StatusCodes.Status201Created, result.User);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request)
    {
        if (!IsTrustedRequest()) return StatusCode(403);
        var session = await sessions.LoginAsync(request);
        if (session is null) return Unauthorized(new { message = "Invalid email or password, or account temporarily locked." });
        await sessions.LogoutAsync(Request.Cookies[CookieName]);
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh()
    {
        if (!IsTrustedRequest()) return StatusCode(403);
        var session = await sessions.RefreshAsync(Request.Cookies[CookieName]);
        if (session is null)
        {
            Response.Cookies.Delete(CookieName, CookieOptions());
            return Unauthorized();
        }
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (!IsTrustedRequest()) return StatusCode(403);
        await sessions.LogoutAsync(Request.Cookies[CookieName]);
        Response.Cookies.Delete(CookieName, CookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me()
    {
        var user = await users.FindByIdAsync(currentUser.RequiredUserId);
        return user is null ? Unauthorized() : Ok(new AuthUserResponse(user.Id, user.Email!, user.EmailConfirmed));
    }

    private bool IsTrustedRequest()
    {
        // A custom header prevents simple cross-origin cookie requests; no permissive CORS is enabled.
        if (Request.Headers["X-JobHunt-CSRF"] != "1") return false;
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true; // Non-browser API clients.
        var sameOrigin = $"{Request.Scheme}://{Request.Host}";
        var allowed = configuration.GetSection("Auth:AllowedOrigins").Get<string[]>() ?? [];
        return origin == sameOrigin || allowed.Contains(origin, StringComparer.Ordinal);
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
        Path = "/api/auth", IsEssential = true
    };

    private void SetRefreshCookie(IssuedSession session)
    {
        var options = CookieOptions();
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(session.RefreshExpiresAtUtc, DateTimeKind.Utc));
        Response.Cookies.Append(CookieName, session.RefreshToken, options);
    }
}
