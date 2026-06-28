using System.Security.Cryptography;
using System.Text;
using ChatRoom.Data;
using ChatRoom.Models;
using ChatRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ChatDbContext dbContext, JwtTokenService tokenService, IMemoryCache cache) : ControllerBase
{
    private const string ApiAudience = "ChatRoomClients";
    private const string PkceCachePrefix = "pkce:";

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("請提供使用者名稱");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("請提供密碼");

        var username = request.Username.Trim();
        var normalizedUsername = UsernameNormalizer.Normalize(username);

        var existingUser = dbContext.Users.FirstOrDefault(u => u.Username.ToLower() == normalizedUsername);
        if (existingUser == null)
            return Unauthorized("帳號不存在");

        if (!Services.PasswordHasher.Verify(request.Password, existingUser.PasswordHash))
            return Unauthorized("密碼錯誤");

        var canonicalUsername = UsernameNormalizer.Normalize(existingUser.Username);
        var token = tokenService.GenerateApiToken(canonicalUsername);
        SetAuthCookie(token);
        return Ok(new { token });
    }

    [HttpGet("session")]
    public IActionResult Session()
    {
        var cookieToken = Request.Cookies["auth_token"];
        if (string.IsNullOrEmpty(cookieToken))
            return Unauthorized();

        var username = tokenService.ValidateApiToken(cookieToken);
        if (username == null)
            return Unauthorized();

        var newToken = tokenService.GenerateApiToken(username);
        SetAuthCookie(newToken);
        return Ok(new { token = newToken });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("auth_token", new CookieOptions { Path = "/" });
        return NoContent();
    }

    private void SetAuthCookie(string token)
    {
        Response.Cookies.Append("auth_token", token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = false,
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });
    }

    [HttpPost("hub-token")]
    [Authorize]
    [EnableRateLimiting("HubTokenPolicy")]
    public IActionResult HubToken()
    {
        var isApiToken = User.HasClaim("aud", ApiAudience);
        if (!isApiToken)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "無效的驗證權杖來源" });

        var username = UsernameNormalizer.Normalize(User.Identity?.Name ?? "Anonymous");
        var hubToken = tokenService.GenerateHubToken(username);
        return Ok(new { token = hubToken });
    }

    /// <summary>
    /// PKCE 第一步：驗證 API Token，存入 code_challenge，回傳一次性 auth_code（TTL 60s）
    /// </summary>
    [HttpPost("hub-token/challenge")]
    [Authorize]
    [EnableRateLimiting("HubTokenPolicy")]
    public IActionResult HubTokenChallenge([FromBody] PkceChallengeRequest request)
    {
        var isApiToken = User.HasClaim("aud", ApiAudience);
        if (!isApiToken)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "無效的驗證權杖來源" });

        if (string.IsNullOrWhiteSpace(request.CodeChallenge))
            return BadRequest(new { message = "缺少 code_challenge" });

        var username = UsernameNormalizer.Normalize(User.Identity?.Name ?? "Anonymous");
        var authCode = Guid.NewGuid().ToString("N");

        cache.Set(
            $"{PkceCachePrefix}{authCode}",
            (username, request.CodeChallenge),
            TimeSpan.FromSeconds(60));

        return Ok(new { auth_code = authCode });
    }

    /// <summary>
    /// PKCE 第二步：驗證 code_verifier，防重放後回傳短效 Hub Token
    /// </summary>
    [HttpPost("hub-token/exchange")]
    public IActionResult HubTokenExchange([FromBody] PkceExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AuthCode) || string.IsNullOrWhiteSpace(request.CodeVerifier))
            return BadRequest(new { message = "缺少必要參數" });

        var cacheKey = $"{PkceCachePrefix}{request.AuthCode}";
        if (!cache.TryGetValue(cacheKey, out (string Username, string CodeChallenge) entry))
            return BadRequest(new { message = "無效或已過期的 auth_code" });

        cache.Remove(cacheKey);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(request.CodeVerifier));
        var computedChallenge = Base64UrlEncoder.Encode(hash);

        if (computedChallenge != entry.CodeChallenge)
            return BadRequest(new { message = "code_verifier 驗證失敗" });

        var hubToken = tokenService.GenerateHubToken(entry.Username);
        return Ok(new { token = hubToken });
    }
}
