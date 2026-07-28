using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    /// <summary>
    /// Tracks the overall compliance status of a user or entity
    /// FICA Compliance Enterprise Model - Part of the compliance system
    /// </summary>
    public class ComplianceStatus
    {
        [Key]
        public int ComplianceStatusId { get; set; }

        /// <summary>
        /// Reference to the user being tracked
        /// </summary>
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        /// <summary>
        /// Optional reference to department for department-level compliance
        /// </summary>
        [ForeignKey("Department")]
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>
        /// Overall compliance status: Compliant, Non-Compliant, Partial, Pending, Review-Required
        /// </summary>
        [Required]
        [StringLength(50)]
        public string OverallStatus { get; set; } = "Pending";

        /// <summary>
        /// Risk level assessment: Low, Medium, High, Critical
        /// </summary>
        [Required]
        [StringLength(20)]
        public string RiskLevel { get; set; } = "Medium";

        /// <summary>
        /// Compliance category: Individual, Business, Trust, NGO, Government
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ComplianceCategory { get; set; } = "Individual";

        // Document Statistics
        public int TotalRequiredDocuments { get; set; }
        public int UploadedDocuments { get; set; }
        public int CompliantDocuments { get; set; }
        public int NonCompliantDocuments { get; set; }
        public int ExpiredDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public int NotCertifiedDocuments { get; set; }
        public int PendingReviewDocuments { get; set; }

        // Compliance Scoring
        public int CompliancePercentage { get; set; } // 0-100
        public int OverallRiskScore { get; set; } // 0-100 (higher = more risky)
        public decimal ComplianceScore { get; set; } // 0-100 with decimal precision

        // Timeline
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime? NextReviewDate { get; set; }
        public DateTime? ComplianceDeadline { get; set; }

        // Compliance Flags
        public bool RequiresEnhancedDueDiligence { get; set; } = false;
        public bool IsSuspicious { get; set; } = false;
        public bool IsPEP { get; set; } = false; // Politically Exposed Person
        public bool HasSanctionFlag { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Metadata
        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        // Navigation properties
        public ICollection<DocumentComplianceCheck> DocumentChecks { get; set; } = new List<DocumentComplianceCheck>();
        public ICollection<ComplianceHistory> ComplianceHistories { get; set; } = new List<ComplianceHistory>();
        public ICollection<ComplianceAlert> ComplianceAlerts { get; set; } = new List<ComplianceAlert>();
        public ICollection<ComplianceAuditLog> AuditLogs { get; set; } = new List<ComplianceAuditLog>();
        public ICollection<ComplianceResult> ComplianceResults { get; set; } = new List<ComplianceResult>();
    }
}
