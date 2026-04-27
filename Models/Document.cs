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

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey("DocumentType")]
        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        public DocumentBlob DocumentBlob { get; set; } = null!;

        public ICollection<CertificationDetails> CertificationDetails { get; set; } = new List<CertificationDetails>();

        public ICollection<DocumentStatusHistory> DocumentStatusHistories { get; set; } = new List<DocumentStatusHistory>();

        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
    }
}
