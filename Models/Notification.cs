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

        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();

        public ICollection<NotificationHistory> NotificationHistories { get; set; } = new List<NotificationHistory>();
    }
}
