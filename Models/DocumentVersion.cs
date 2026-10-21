using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentVersion
    {
        [Key]
        public int DocumentVersionId { get; set; }

        [ForeignKey(nameof(Document))]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        public int VersionNumber { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public byte[] EncryptedFile { get; set; } = Array.Empty<byte>();

        [Required]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        public string UploadedByUserId { get; set; } = string.Empty;
        public User UploadedByUser { get; set; } = null!;

        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

        [StringLength(500)]
        public string? FlagReason { get; set; }

        [StringLength(30)]
        public string? ComplianceDecision { get; set; }

        public string? ComplianceReviewerUserId { get; set; }
        public User? ComplianceReviewerUser { get; set; }

        [StringLength(1000)]
        public string? ComplianceReason { get; set; }

        public DateTimeOffset? ComplianceDecisionAt { get; set; }
    }
}
