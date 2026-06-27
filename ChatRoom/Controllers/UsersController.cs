using ChatRoom.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(ChatDbContext dbContext, HybridCache cache) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser(string username, CancellationToken cancellationToken)
    {
        var cacheKey = $"user-profile-{username.ToLower()}";

        var userProfile = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await dbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), cancel),
            cancellationToken: cancellationToken
        );

        if (userProfile == null)
            return NotFound(new { message = "找不到該使用者" });

        return Ok(userProfile);
    }
}
