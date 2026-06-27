namespace ChatRoom.Models
{
    public class Friendship
    {
        public int Id { get; set; }
        public string User1 { get; set; } = string.Empty; // 使用者 A (Username)
        public string User2 { get; set; } = string.Empty; // 使用者 B (Username)
    }
}
