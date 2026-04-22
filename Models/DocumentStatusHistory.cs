using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentStatusHistory
    {
        [Key]
        public int StatusHistoryId { get; set; }

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        [Required]
        public string StatusName { get; set; } = string.Empty;

        public TimeOnly TimeStamp { get; set; } = new TimeOnly();

        public DateOnly Date { get; set; } = new DateOnly();
    }
}
