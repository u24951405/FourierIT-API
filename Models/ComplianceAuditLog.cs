using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    /// <summary>
    /// Audit log for all compliance system actions for accountability
    /// FICA Compliance Enterprise Model
    /// </summary>
    public class ComplianceAuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        [ForeignKey("ComplianceStatus")]
        public int ComplianceStatusId { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; } = null!;

        // Action Details
        [Required]
        [StringLength(100)]
        public string ActionType { get; set; } = string.Empty; // "CheckCreated", "StatusUpdated", "DocumentApproved", "AlertCreated", etc.

        [Required]
        [StringLength(500)]
        public string ActionDescription { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Details { get; set; }

        // User Action
        [ForeignKey("PerformedByUser")]
        [Required]
        public string PerformedBy { get; set; } = string.Empty;
        public User PerformedByUser { get; set; } = null!;

        [StringLength(50)]
        public string? UserRole { get; set; } // "Admin", "Manager", "Reviewer", "System"

        // Timestamp
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        // Related Entities
        [ForeignKey("Document")]
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        [ForeignKey("CheckRecord")]
        public int? DocumentCheckId { get; set; }
        public DocumentComplianceCheck? CheckRecord { get; set; }

        // Data Changes
        [StringLength(500)]
        public string? OldValue { get; set; } // JSON serialized
        [StringLength(500)]
        public string? NewValue { get; set; } // JSON serialized

        // IP Address & Location
        [StringLength(45)]
        public string? IpAddress { get; set; } // IPv4 or IPv6

        [StringLength(100)]
        public string? UserAgent { get; set; }

        // Compliance Impact
        public bool AffectsCompliance { get; set; } = false;
        public bool RequiresApproval { get; set; } = false;
        public bool IsApproved { get; set; } = false;

        // Status
        public bool IsSuccessful { get; set; } = true;

        [StringLength(300)]
        public string? ErrorMessage { get; set; }

        // Reversibility
        public bool CanBeReversed { get; set; } = false;
        public bool HasBeenReversed { get; set; } = false;
        public int? ReversedByAuditLogId { get; set; }

        // Batch Operation
        public int? BatchId { get; set; }
        public bool IsPartOfBatch { get; set; } = false;

        // Duration (for long operations)
        public long? DurationMilliseconds { get; set; }

        // External System Reference
        [StringLength(100)]
        public string? ExternalReference { get; set; } // For integrations with other systems
    }
}
