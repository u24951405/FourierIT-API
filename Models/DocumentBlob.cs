using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentBlob
    {
        [Key]
        public int DocumentBlobId { get; set; }

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        //FileHash is a string that represents the hash of the file content, used for integrity verification and to detect duplicate files.
        public string FileHash { get; set; } = null!;

        public int VersionNumber { get; set; }

        //FileData is a byte array that stores the actual binary content of the file, allowing for efficient storage and retrieval of document data.
        [StringLength(50485760)] // 50 MB limit for file size
        public byte[] FileData { get; set; } = null!;

        public ICollection<BlobHistory> BlobHistories { get; set; } = new List<BlobHistory>();
    }
}
