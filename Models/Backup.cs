using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class Backup
    {
        [Key]
        public int BackupId { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public DateTime DateBackedUp { get; set; }

        [Required]
        public bool IsManualBackup { get; set; } = false;
    }
}
