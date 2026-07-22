using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Backup
    {
        [Key]
        public int BackupId { get; set; }

        // Track which user created the backup (Identity user id is string)
        [ForeignKey(nameof(User))]
        public string? UserId { get; set; }

        public virtual User? User { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DateBackedUp { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        public bool IsManualBackup { get; set; } = false;
    }
}
