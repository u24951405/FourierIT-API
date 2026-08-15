using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentStatusHistory
    {
        [Key]
        public int StatusHistoryId { get; set; }

        [ForeignKey("Document")]
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        [Required]
        [StringLength(20)]
        public string StatusName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DateArchived { get; set; } = DateTimeOffset.UtcNow;
    }
}
