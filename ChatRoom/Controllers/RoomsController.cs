using ChatRoom.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomsController(ChatDbContext dbContext, HybridCache cache) : ControllerBase
{
    [HttpGet("{roomName}/messages")]
    public async Task<IActionResult> GetMessages(string roomName, CancellationToken cancellationToken)
    {
        var cacheKey = $"messages-{roomName.ToLower()}";

        var messages = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await dbContext.ChatMessages
                .Where(m => m.RoomName != null && m.RoomName.ToLower() == roomName.ToLower())
                .OrderByDescending(m => m.Timestamp)
                .Take(50)
                .OrderBy(m => m.Timestamp)
                .ToListAsync(cancel),
            cancellationToken: cancellationToken
        );

        return Ok(messages);
    }
}
