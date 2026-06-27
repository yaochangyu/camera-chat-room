using ChatRoom.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(ChatDbContext dbContext, HybridCache cache) : ControllerBase
{
    [HttpGet("dm/{targetUsername}")]
    public async Task<IActionResult> GetDmMessages(string targetUsername, CancellationToken cancellationToken)
    {
        var sender = User.Identity?.Name;
        if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(targetUsername))
            return BadRequest("無效的使用者身分");

        var sortedUsers = new[] { sender.ToLower(), targetUsername.ToLower() };
        Array.Sort(sortedUsers);
        var cacheKey = $"messages-dm-{sortedUsers[0]}-{sortedUsers[1]}";

        var messages = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await dbContext.ChatMessages
                .Where(m => m.RoomName == null &&
                            ((m.Username.ToLower() == sender.ToLower() && m.ReceiverUsername!.ToLower() == targetUsername.ToLower()) ||
                             (m.Username.ToLower() == targetUsername.ToLower() && m.ReceiverUsername!.ToLower() == sender.ToLower())))
                .OrderByDescending(m => m.Timestamp)
                .Take(50)
                .OrderBy(m => m.Timestamp)
                .ToListAsync(cancel),
            cancellationToken: cancellationToken
        );

        return Ok(messages);
    }
}
