using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset ExpiryDate { get; set; } = DateTimeOffset.Now;

        [Required]
        [StringLength(20)]
        public string CurrentStatus { get; set; } = string.Empty;

        [Required]
        public bool IsCertified { get; set; } = false;

        [Required]
        public byte[] EncryptedFileData { get; set; } = null;

        [Required]
        public long FileSizeBytes { get; set; }

        [Required]
        [StringLength(50)]
        public string EncryptionAlgorithm { get; set; } = "AES-256";

        [Required]
        public bool IsEncrypted { get; set; } = true;

        [Required]
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastModified { get; set; }

        public DateTime? LastAccessedDate { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        [ForeignKey("DocumentType")]
        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        public DocumentBlob DocumentBlob { get; set; } = null!;

        public ICollection<CertificationDetails> CertificationDetails { get; set; } = new List<CertificationDetails>();

        public ICollection<DocumentStatusHistory> DocumentStatusHistories { get; set; } = new List<DocumentStatusHistory>();

        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();

        public ICollection<DocumentAccess> SharedWith { get; set; } = new List<DocumentAcess>();

        public ICollection<DocumentAccessLog> AccessLogs { get; set; } = new List<DocumentAcessLog>();
    }
}
