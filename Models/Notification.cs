using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string subject { get; set; } = string.Empty;

        [Required]
        public string message { get; set; } = string.Empty;

        public TimeOnly sentTime { get; set; } = new TimeOnly();

        public bool isRead { get; set; } = false;

        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    }
}
