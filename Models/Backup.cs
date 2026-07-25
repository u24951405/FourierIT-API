using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Backup
    {
        [Key]
        public int BackupId { get; set; }

        public string? UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DateBackedUp { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        public bool IsManualBackup { get; set; } = false;
    }
}
