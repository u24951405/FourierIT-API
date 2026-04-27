using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Backup
    {
        [Key]
        public int BackupId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DateBackedUp { get; set; } = DateTimeOffset.Now;

        [Required]
        public bool IsManualBackup { get; set; } = false;
    }
}
