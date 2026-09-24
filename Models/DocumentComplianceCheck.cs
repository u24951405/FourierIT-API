using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    /// <summary>
    /// Tracks detailed compliance check results for individual documents
    /// FICA Compliance Enterprise Model
    /// </summary>
    public class DocumentComplianceCheck
    {
        [Key]
        public int CheckId { get; set; }

        [ForeignKey("Document")]
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        [ForeignKey("ComplianceStatus")]
        public int ComplianceStatusId { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; } = null!;

        // Check Result
        [Required]
        [StringLength(50)]
        public string CheckStatus { get; set; } = "Compliant"; // Compliant, Non-Compliant, Warning, Pending

        // Detailed Reasons for Non-Compliance
        [StringLength(500)]
        public string? NonComplianceReason { get; set; }

        // Individual Checks
        public bool IsExpiryValid { get; set; } = true;
        // Derived from the document type validity policy and never persisted in the database,
        // because it can go stale when a Super Admin changes the document-type settings.
        [NotMapped]
        public bool NeverExpires { get; set; } = false;
        public DateTime? ExpiryCheckDate { get; set; }
        public int? DaysUntilExpiry { get; set; }

        public bool IsCertified { get; set; } = true;
        [StringLength(100)]
        public string? CertificationType { get; set; } // "CapeTownNotary", "Commissioner", "Certified True Copy", etc.

        public bool IsRecent { get; set; } = true;
        public int? DocumentAgeInMonths { get; set; }

        public bool IsEncrypted { get; set; } = true;
        public bool IsVirusFree { get; set; } = true;

        // Quality Assessment
        public int QualityScore { get; set; } = 100; // 0-100
        public bool IsHighQuality { get; set; } = true;
        public bool IsLegible { get; set; } = true;

        // Manual Review Flags
        public bool RequiresManualReview { get; set; } = false;
        [StringLength(500)]
        public string? ManualReviewReason { get; set; }

        public bool IsManuallyApproved { get; set; } = false;
        [ForeignKey("ManualReviewedByUser")]
        public string? ManuallyReviewedBy { get; set; }
        public User? ManualReviewedByUser { get; set; }
        public DateTime? ManualReviewDate { get; set; }

        // Remediation & Actions
        [StringLength(150)]
        public string? RemediationAction { get; set; } // "Reupload", "Get Certified", "Renew Document", etc.

        public DateTime? ActionDueDate { get; set; }
        public bool ActionCompleted { get; set; } = false;
        public DateTime? ActionCompletedDate { get; set; }

        // Risk Assessment
        public int IndividualRiskScore { get; set; } = 0; // 0-100
        [StringLength(100)]
        public string? RiskCategory { get; set; } // "Low", "Medium", "High", "Critical"

        // Metadata
        [StringLength(500)]
        public string? Details { get; set; }

        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CheckedByUser")]
        public string? CheckedBy { get; set; }
        public User? CheckedByUser { get; set; }

        // Compliance Rules Applied
        [ForeignKey("FICARule")]
        public int? AppliedRuleId { get; set; }
        public FICARule? AppliedRule { get; set; }

        // Document Type (for reference - relationship configured in DbContext)
        // Note: Cannot create FK to composite key of DocumentFicaRule
        public int? DocumentTypeId { get; set; }

        // Audit Trail
        public DateTime? LastReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }

        // Batch Processing
        public int? BatchId { get; set; } // For grouped compliance checks
        public bool IsPartOfBatch { get; set; } = false;
    }
}
