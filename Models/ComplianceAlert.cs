using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    /// <summary>
    /// Compliance alerts for issues and actions required
    /// FICA Compliance Enterprise Model
    /// </summary>
    public class ComplianceAlert
    {
        [Key]
        public int AlertId { get; set; }

        [ForeignKey("ComplianceStatus")]
        public int ComplianceStatusId { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; } = null!;

        // Alert Classification
        [Required]
        [StringLength(50)]
        public string AlertType { get; set; } = string.Empty; // "Expired", "Missing", "NotCertified", "LowQuality", "ManualReview", "Suspicious"

        [Required]
        [StringLength(20)]
        public string Severity { get; set; } = "Medium"; // "Critical", "High", "Medium", "Low", "Info"

        // Alert Details
        [Required]
        [StringLength(300)]
        public string AlertMessage { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Details { get; set; }

        // Document Related
        [ForeignKey("Document")]
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        [StringLength(100)]
        public string? DocumentName { get; set; }

        [StringLength(100)]
        public string? DocumentType { get; set; }

        // User Related
        [ForeignKey("User")]
        public string? UserId { get; set; }
        public User? User { get; set; }

        // Timeline
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; } // When action is required by
        public DateTime? ResolvedAt { get; set; }

        // Status
        public bool IsResolved { get; set; } = false;
        public bool IsAcknowledged { get; set; } = false;
        public DateTime? AcknowledgedAt { get; set; }

        [ForeignKey("AcknowledgedByUser")]
        public string? AcknowledgedBy { get; set; }
        public User? AcknowledgedByUser { get; set; }

        // Resolution
        [StringLength(500)]
        public string? ResolutionNotes { get; set; }

        [ForeignKey("ResolvedByUser")]
        public string? ResolvedBy { get; set; }
        public User? ResolvedByUser { get; set; }

        // Action Items
        [StringLength(200)]
        public string? RequiredAction { get; set; }

        public bool ActionRequiredFromUser { get; set; } = false;
        public bool ActionRequiredFromAdmin { get; set; } = false;

        // Notification
        public bool NotificationSent { get; set; } = false;
        public int NotificationAttempts { get; set; } = 0;
        public DateTime? LastNotificationDate { get; set; }

        // Priority & Escalation
        public int Priority { get; set; } = 0; // 1 = highest
        public bool IsEscalated { get; set; } = false;
        public DateTime? EscalatedAt { get; set; }

        [ForeignKey("EscalatedToUser")]
        public string? EscalatedTo { get; set; }
        public User? EscalatedToUser { get; set; }

        // Automatic Remediation
        public bool CanBeAutoResolved { get; set; } = false;
        public bool WasAutoResolved { get; set; } = false;

        // Related Alerts
        public int? RelatedAlertId { get; set; } // Link to related alert
    }
}
