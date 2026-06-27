using System;

namespace ChatRoom.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;       // 傳送者 (Sender)
        public string? ReceiverUsername { get; set; }               // 接收者 (Receiver, 僅私聊有值)
        public string? RoomName { get; set; }                      // 房間名稱 (僅群組有值)
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
