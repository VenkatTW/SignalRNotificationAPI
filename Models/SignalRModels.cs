using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SignalRNotificationAPI.Models
{
    public class UserConnection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ConnectionId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ServerInstance { get; set; } = string.Empty;

        public DateTime ConnectedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public bool IsActive { get; set; }
    }

    public class PersistedMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string TargetUserId { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SenderUserId { get; set; }

        [MaxLength(50)]
        public string MessageType { get; set; } = "Notification";

        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public bool IsDelivered { get; set; }
        public bool IsPersistent { get; set; } = true;
        public DateTime? ExpiresAt { get; set; }

        [MaxLength(500)]
        public string? Metadata { get; set; }
    }

    public class MessageDeliveryStatus
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ConnectionId { get; set; } = string.Empty;

        public DateTime AttemptedAt { get; set; }
        public bool IsSuccessful { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        [ForeignKey("MessageId")]
        public PersistedMessage Message { get; set; } = null!;
    }

    public class UserSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        public DateTime SessionStart { get; set; }
        public DateTime? SessionEnd { get; set; }
        public bool IsActive { get; set; }

        [MaxLength(50)]
        public string ServerInstance { get; set; } = string.Empty;

        public int ConnectionCount { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
