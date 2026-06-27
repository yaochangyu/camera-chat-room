using System;

namespace ChatRoom.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;       // 唯一帳號
        public string Email { get; set; } = string.Empty;          // 信箱
        public string Bio { get; set; } = string.Empty;            // 個人簡介
        public string AvatarUrl { get; set; } = string.Empty;       // 頭像 URL
        public string StatusMessage { get; set; } = string.Empty;  // 自訂狀態文字
        public string PasswordHash { get; set; } = string.Empty;   // PBKDF2-SHA256 雜湊
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 註冊時間
    }
}
