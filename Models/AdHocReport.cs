using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class AdHocReport
    {
        [Key]
        public int AdHocReportId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public DateTime DateFrom { get; set; }

        [Required]
        public DateTime DateTo { get; set; }

        [Required]
        [MaxLength(1000)]
        public string FocusAreas { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ExportFormat { get; set; } = "PDF";

        [Required]
        [MaxLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CreatedByName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "ready";

        public long SizeKb { get; set; }
    }
}