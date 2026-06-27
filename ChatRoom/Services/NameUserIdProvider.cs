using Microsoft.AspNetCore.SignalR;

namespace ChatRoom.Services
{
    public class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // 使用一致的 username 規則作為 SignalR User Identifier，避免 Bob/bob 不一致
            return UsernameNormalizer.NormalizeNullable(connection.User?.Identity?.Name);
        }
    }
}
