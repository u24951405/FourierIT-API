using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class NotificationHistory
    {
        [Key]
        public int NotificationHistoryId { get; set; }

        [Required]
        [StringLength(50)]
        public string DeliveryMethod { get; set; } = string.Empty;

        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

        // Foreign key to Notification
        [ForeignKey("Notification")]
        public int NotificationId { get; set; }

        public Notification Notification { get; set; } = null!;
    }
}
