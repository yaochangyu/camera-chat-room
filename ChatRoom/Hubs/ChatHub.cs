using System.Collections.Concurrent;
using ChatRoom.Data;
using ChatRoom.Models;
using ChatRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChatRoom.Hubs
{
    [Authorize]
    public class ChatHub : Hub<IChatClient>
    {
        private readonly ChatDbContext _dbContext;
        private readonly HybridCache _cache;

        // Key: 正規化 username，Value: 目前連線數（同帳號多 Tab 各算一條）
        private static readonly ConcurrentDictionary<string, int> _onlineUsers = new();

        public static bool IsUserOnline(string username)
        {
            var canonicalUsername = UsernameNormalizer.NormalizeNullable(username);
            return canonicalUsername != null && _onlineUsers.ContainsKey(canonicalUsername);
        }

        public static string[] GetOnlineUsers()
        {
            return _onlineUsers.Keys.ToArray();
        }

        public ChatHub(ChatDbContext dbContext, HybridCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task SendMessageToRoom(string roomName, string message)
        {
            var user = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name) ?? "anonymous";

            var chatMsg = new ChatMessage
            {
                Username = user,
                RoomName = roomName,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            _dbContext.ChatMessages.Add(chatMsg);
            await _dbContext.SaveChangesAsync();

            var cacheKey = $"messages-{roomName.ToLower()}";
            await _cache.RemoveAsync(cacheKey);

            await Clients.Group(roomName).ReceiveMessage(user, message);
        }

        public async Task SendPrivateMessage(string targetUser, string message)
        {
            var sender = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
            var canonicalTargetUser = UsernameNormalizer.NormalizeNullable(targetUser);
            if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(canonicalTargetUser)) return;

            var chatMsg = new ChatMessage
            {
                Username = sender,
                ReceiverUsername = canonicalTargetUser,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            _dbContext.ChatMessages.Add(chatMsg);
            await _dbContext.SaveChangesAsync();

            // 雙方帳號排序後拼接確保 alice-bob 與 bob-alice 共用同一個 cache key
            var sortedUsers = new[] { sender, canonicalTargetUser };
            Array.Sort(sortedUsers);
            await _cache.RemoveAsync($"messages-dm-{sortedUsers[0]}-{sortedUsers[1]}");

            // Clients.User 會推送到該帳號的所有連線（多 Tab 全收到）
            await Clients.User(canonicalTargetUser).ReceivePrivateMessage(sender, message);
        }

        public async Task JoinRoom(string roomName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).UserJoined(Context.ConnectionId, roomName);
            await Clients.Caller.ReceiveNotification($"您已成功加入房間: {roomName}");
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).UserLeft(Context.ConnectionId, roomName);
            await Clients.Caller.ReceiveNotification($"您已離開房間: {roomName}");
        }

        public override async Task OnConnectedAsync()
        {
            // Audience 限定 Hub Token 才能連線，避免長效 API Token 直接建立 WebSocket
            var isHubToken = Context.User?.HasClaim("aud", "ChatRoomSignalRHub") ?? false;
            if (!isHubToken)
            {
                Context.Abort();
                return;
            }

            var username = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
            if (!string.IsNullOrEmpty(username))
            {
                bool isNewlyOnline = false;
                _onlineUsers.AddOrUpdate(username,
                    k => { isNewlyOnline = true; return 1; },
                    (k, val) => val + 1);

                await BroadcastOnlineUsers();

                if (isNewlyOnline)
                    await NotifyFriendsStatusChanged(username, true);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var username = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
            if (!string.IsNullOrEmpty(username))
            {
                bool isFullyOffline = false;
                // CAS 重試迴圈：TryUpdate 在多連線同時斷線時可能被其他執行緒搶先，必須循環直到成功
                while (true)
                {
                    if (!_onlineUsers.TryGetValue(username, out var count)) break;

                    if (count <= 1)
                    {
                        if (_onlineUsers.TryRemove(username, out _)) { isFullyOffline = true; break; }
                        continue;
                    }

                    if (_onlineUsers.TryUpdate(username, count - 1, count)) break;
                }

                await BroadcastOnlineUsers();

                if (isFullyOffline)
                    await NotifyFriendsStatusChanged(username, false);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task UpdateStatusMessage(string message)
        {
            var username = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
            if (string.IsNullOrEmpty(username)) return;

            var userEntity = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            if (userEntity != null)
            {
                userEntity.StatusMessage = message;
                await _dbContext.SaveChangesAsync();
                await _cache.RemoveAsync($"user-profile-{username.ToLower()}");
                await NotifyFriendsStatusChanged(username, true);
            }
        }

        private async Task NotifyFriendsStatusChanged(string username, bool isOnline)
        {
            try
            {
                var friendNames = await GetFriendNamesAsync(username);

                string? statusMessage = null;
                if (isOnline)
                {
                    var userEntity = await _dbContext.Users
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
                    statusMessage = userEntity?.StatusMessage;
                }

                foreach (var friendName in friendNames)
                {
                    var canonicalFriendName = UsernameNormalizer.Normalize(friendName);
                    if (IsUserOnline(canonicalFriendName))
                        await Clients.User(canonicalFriendName).FriendStatusChanged(username, isOnline, statusMessage);
                }
            }
            catch (Exception ex)
            {
                // 捕捉例外避免通知失敗中斷整個 OnConnected/OnDisconnected 事件
                Console.WriteLine($"通知好友狀態變更失敗: {ex.Message}");
            }
        }

        private async Task<List<string>> GetFriendNamesAsync(string username)
        {
            var friendships = await _dbContext.Friendships
                .Where(f => f.User1.ToLower() == username.ToLower() || f.User2.ToLower() == username.ToLower())
                .ToListAsync();

            return friendships
                .Select(f => f.User1.ToLower() == username.ToLower() ? f.User2 : f.User1)
                .Distinct()
                .ToList();
        }

        private async Task BroadcastOnlineUsers()
        {
            await Clients.All.UpdateUserList(_onlineUsers.Keys.ToArray());
        }
    }
}
