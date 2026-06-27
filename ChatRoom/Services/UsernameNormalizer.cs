namespace ChatRoom.Services
{
    public static class UsernameNormalizer
    {
        public static string Normalize(string username)
        {
            return username.Trim().ToLowerInvariant();
        }

        public static string? NormalizeNullable(string? username)
        {
            return string.IsNullOrWhiteSpace(username) ? null : Normalize(username);
        }
    }
}
