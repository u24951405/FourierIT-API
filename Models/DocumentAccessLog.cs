using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Headers;

namespace FourierIT_API.Models
{
    public class DocumentAccessLog
    {
        [Key]
        public int DocumentAccessLogId { get; set; }

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        [ForeignKey("AccessedByUser")]
        public string AccessedByUserId { get; set; } = string.Empty;
        public User AccessedByUser { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [Required]
        public DateTime AccessDateTime { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string IPAddress { get; set; } = string.Empty;

        [StringLength(255)]
        public string UserAgent { get; set; } = string.Empty;
    }
}
