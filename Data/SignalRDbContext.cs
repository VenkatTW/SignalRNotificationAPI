using Microsoft.EntityFrameworkCore;
using SignalRNotificationAPI.Models;

namespace SignalRNotificationAPI.Data
{
    public class SignalRDbContext : DbContext
    {
        public SignalRDbContext(DbContextOptions<SignalRDbContext> options) : base(options)
        {
        }

        public DbSet<UserConnection> UserConnections { get; set; }
        public DbSet<PersistedMessage> PersistedMessages { get; set; }
        public DbSet<MessageDeliveryStatus> MessageDeliveryStatuses { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserConnection configuration
            modelBuilder.Entity<UserConnection>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ConnectionId).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.IsActive });
                entity.HasIndex(e => e.LastHeartbeat);
            });

            // PersistedMessage configuration
            modelBuilder.Entity<PersistedMessage>(entity =>
            {
                entity.HasIndex(e => e.TargetUserId);
                entity.HasIndex(e => new { e.TargetUserId, e.IsDelivered });
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => new { e.IsDelivered, e.ExpiresAt });
            });

            // MessageDeliveryStatus configuration
            modelBuilder.Entity<MessageDeliveryStatus>(entity =>
            {
                entity.HasIndex(e => e.MessageId);
                entity.HasIndex(e => e.ConnectionId);
                entity.HasIndex(e => new { e.MessageId, e.IsSuccessful });
            });

            // UserSession configuration
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.UserId, e.IsActive });
                entity.HasIndex(e => e.SessionStart);
                entity.HasIndex(e => e.LastActivity);
            });
        }
    }
}
