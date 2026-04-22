using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentBlob
    {
        [Key]
        public int DocumentBlobId { get; set; }
        public byte[] BlobData { get; set; } = Array.Empty<byte>();

        public string FileHash { get; set; } = string.Empty;

        public decimal FileSize { get; set; } = 0;

        public string VersionNumber { get; set; } = string.Empty;

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        public ICollection<BlobHistory> BlobHistories { get; set; } = new List<BlobHistory>();
    }
}
