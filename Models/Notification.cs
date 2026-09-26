using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        // e.g. "DocumentApproved", "DocumentRejected"
        [StringLength(50)]
        public string? Category { get; set; }

        // The document this notification is about, if any. Not a foreign key so notifications survive document deletion.
        public int? DocumentId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();

        public ICollection<NotificationHistory> NotificationHistories { get; set; } = new List<NotificationHistory>();
    }
}
