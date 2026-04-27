using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Models
{
    public class BlobHistory
    {
        [Key]
        public int BlobHistoryId { get; set; }

        public DateTimeOffset ArchivedDate { get; set; } = DateTimeOffset.Now;

        [Required]
        [StringLength(50)]
        public string ActionTaken { get; set; } = string.Empty;

        [ForeignKey("DocumentBlob")]
        public int DocumentBlobId { get; set; }
        public DocumentBlob DocumentBlob { get; set; } = null!;
    }
}
