using ChatRoom.Data;
using ChatRoom.Hubs;
using ChatRoom.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController(ChatDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        var currentUser = User.Identity?.Name;
        if (string.IsNullOrEmpty(currentUser)) return Unauthorized();

        var friendships = await dbContext.Friendships
            .Where(f => f.User1.ToLower() == currentUser.ToLower() || f.User2.ToLower() == currentUser.ToLower())
            .ToListAsync();

        var friendNamesCanonical = friendships
            .Select(f => f.User1.ToLower() == currentUser.ToLower() ? f.User2.ToLower() : f.User1.ToLower())
            .Distinct()
            .ToList();

        var friendsProfile = await dbContext.Users
            .Where(u => friendNamesCanonical.Contains(u.Username.ToLower()))
            .ToListAsync();

        var result = friendsProfile.Select(f => new
        {
            f.Username,
            f.Email,
            f.Bio,
            f.StatusMessage,
            IsOnline = ChatHub.IsUserOnline(f.Username)
        });

        return Ok(result);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddFriend([FromBody] AddFriendRequest request)
    {
        var currentUser = User.Identity?.Name;
        if (string.IsNullOrEmpty(currentUser)) return Unauthorized();

        var target = request.TargetUsername?.Trim();
        if (string.IsNullOrEmpty(target)) return BadRequest("請提供目標使用者名稱");

        if (currentUser.ToLower() == target.ToLower()) return BadRequest("不能加自己為好友");

        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == target.ToLower());
        if (targetUser == null)
            return NotFound(new { message = "找不到該目標使用者" });

        var alreadyFriend = await dbContext.Friendships.AnyAsync(f =>
            (f.User1.ToLower() == currentUser.ToLower() && f.User2.ToLower() == target.ToLower()) ||
            (f.User1.ToLower() == target.ToLower() && f.User2.ToLower() == currentUser.ToLower())
        );

        if (!alreadyFriend)
        {
            dbContext.Friendships.Add(new Friendship
            {
                User1 = currentUser,
                User2 = targetUser.Username
            });
            await dbContext.SaveChangesAsync();
        }

        return Ok(new { message = "成功加為好友！" });
    }
}
