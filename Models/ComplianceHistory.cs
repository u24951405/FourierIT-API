using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    /// <summary>
    /// Tracks compliance status changes and history for audit trail
    /// FICA Compliance Enterprise Model
    /// </summary>
    public class ComplianceHistory
    {
        [Key]
        public int HistoryId { get; set; }

        [ForeignKey("ComplianceStatus")]
        public int ComplianceStatusId { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; } = null!;

        // Status Change Details
        [Required]
        [StringLength(50)]
        public string PreviousStatus { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string NewStatus { get; set; } = string.Empty;

        // Risk Level Change
        [StringLength(20)]
        public string? PreviousRiskLevel { get; set; }

        [StringLength(20)]
        public string? NewRiskLevel { get; set; }

        // Compliance Score Change
        public int? PreviousComplianceScore { get; set; }
        public int? NewComplianceScore { get; set; }

        // Change Details
        [Required]
        [StringLength(200)]
        public string ChangeReason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Details { get; set; }

        // Document Changes
        [StringLength(100)]
        public string? TriggeringDocument { get; set; } // Document that caused the change

        public int? DocumentsAffected { get; set; } // Count of documents that changed status

        // Timeline
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ChangedByUser")]
        public string? ChangedBy { get; set; }
        public User? ChangedByUser { get; set; }

        // Change Source
        [StringLength(50)]
        public string ChangeSource { get; set; } = "System"; // "System", "Manual", "Automated", "User"

        // Compliance Action Triggered
        public bool ActionTriggered { get; set; } = false;
        [StringLength(200)]
        public string? ActionTriggeredDescription { get; set; } // e.g., "Email sent to user", "Alert created"

        // Approval Status
        public bool RequiresApproval { get; set; } = false;
        public bool IsApproved { get; set; } = false;

        [ForeignKey("ApprovedByUser")]
        public string? ApprovedBy { get; set; }
        public User? ApprovedByUser { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Rollback Capability
        public bool CanBeRolledBack { get; set; } = false;
        public bool HasBeenRolledBack { get; set; } = false;
    }
}
