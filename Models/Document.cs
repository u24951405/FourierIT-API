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
        public string fileName { get; set; } = string.Empty;

        [Required]
        public DateOnly ExpiryDate { get; set; } = new DateOnly();
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsCertified { get; set; } = false;

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey("DocumentType")]
        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        [ForeignKey("DocumentBlob")]
        public int DocumentBlobId { get; set; }
        public DocumentBlob DocumentBlob { get; set; } = null!;

        public CertificationDetails CertificationDetails { get; set; } = null!;

        public ICollection<DocumentStatusHistory> DocumentStatusHistories { get; set; } = new List<DocumentStatusHistory>();

        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
    }
}
