using ChatRoom.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatRoom.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Friendship> Friendships { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}
