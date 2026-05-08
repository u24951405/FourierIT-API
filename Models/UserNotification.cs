using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class UserNotification
    {
        [Key]
        [ForeignKey("Notification")]
        public int NotificationId { get; set; }
        public Notification Notification { get; set; } = null!;

        [Key]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        [Required]
        public bool IsRead { get; set; }
    }
}
