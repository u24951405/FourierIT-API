namespace FourierIT_API.DTOs.Compliance
{
    // ============ DASHBOARD DTOs ============

    /// <summary>
    /// System-wide compliance dashboard
    /// </summary>
    public class ComplianceDashboardDto
    {
        // System Overview
        public int TotalUsers { get; set; }
        public int CompliantUsers { get; set; }
        public int NonCompliantUsers { get; set; }
        public int PartialCompliantUsers { get; set; }
        public int PendingUsers { get; set; }
        public int ReviewRequiredUsers { get; set; }

        // Compliance Metrics
        public decimal OverallCompliancePercentage { get; set; }
        public decimal AverageComplianceScore { get; set; }
        public int TotalDocumentsChecked { get; set; }
        public int TotalCompliantDocuments { get; set; }

        // Risk Metrics
        public int CriticalRiskUsers { get; set; }
        public int HighRiskUsers { get; set; }
        public int MediumRiskUsers { get; set; }
        public int LowRiskUsers { get; set; }

        // Department Statistics
        public List<DepartmentComplianceDto> Departments { get; set; } = new();

        // Alerts & Issues
        public List<ComplianceAlertDto> CriticalAlerts { get; set; } = new();
        public List<ComplianceAlertDto> HighPriorityAlerts { get; set; } = new();
        public int TotalOpenAlerts { get; set; }

        // Trends
        public List<ComplianceTrendDto> ComplianceTrends { get; set; } = new();

        // Compliance Statistics
        public ComplianceStatisticsDto Statistics { get; set; } = new();

        // Last Updated
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Department-level compliance information
    /// </summary>
    public class DepartmentComplianceDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public int TotalMembers { get; set; }
        public int CompliantMembers { get; set; }
        public int NonCompliantMembers { get; set; }
        public int PartialMembers { get; set; }

        public decimal CompliancePercentage { get; set; }
        public decimal AverageRiskScore { get; set; }

        public int OpenAlerts { get; set; }
        public int OverdueActions { get; set; }

        public List<UserComplianceSummaryDto> NonCompliantUsers { get; set; } = new();
    }

    /// <summary>
    /// Individual user compliance status
    /// </summary>
    public class UserComplianceDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Status
        public string OverallStatus { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public string ComplianceCategory { get; set; } = string.Empty;

        // Metrics
        public int TotalRequired { get; set; }
        public int Uploaded { get; set; }
        public int Compliant { get; set; }
        public int NonCompliant { get; set; }
        public int Expired { get; set; }
        public int Missing { get; set; }
        public int NotCertified { get; set; }

        public int CompliancePercentage { get; set; }
        public decimal ComplianceScore { get; set; }
        public int RiskScore { get; set; }

        // Flags
        public bool RequiresEnhancedDueDiligence { get; set; }
        public bool IsPEP { get; set; }
        public bool HasSanctionFlag { get; set; }

        // Timeline
        public DateTime LastChecked { get; set; }
        public DateTime? ComplianceDeadline { get; set; }

        // Issues & Actions
        public List<DocumentComplianceIssueDto> DocumentIssues { get; set; } = new();
        public List<MissingDocumentDto> MissingDocuments { get; set; } = new();
        public List<ComplianceAlertDto> OpenAlerts { get; set; } = new();
    }

    /// <summary>
    /// Document-level compliance issue
    /// </summary>
    public class DocumentComplianceIssueDto
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string? Issue { get; set; }

        public int QualityScore { get; set; }
        public bool IsExpiryValid { get; set; }
        public int? DaysUntilExpiry { get; set; }
        public bool IsCertified { get; set; }
        public bool IsRecent { get; set; }

        public string? RemediationAction { get; set; }
        public DateTime? ActionDueDate { get; set; }

        public bool RequiresManualReview { get; set; }
    }

    /// <summary>
    /// Missing document information
    /// </summary>
    public class MissingDocumentDto
    {
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int DaysOverdue { get; set; }
    }

    /// <summary>
    /// Compliance alert details
    /// </summary>
    public class ComplianceAlertDto
    {
        public int AlertId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public int? DocumentId { get; set; }
        public string? DocumentName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }

        public bool IsResolved { get; set; }
        public string? RequiredAction { get; set; }

        public int Priority { get; set; }
        public bool IsEscalated { get; set; }
    }

    /// <summary>
    /// User compliance summary
    /// </summary>
    public class UserComplianceSummaryDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public int CompliancePercentage { get; set; }
        public int OpenAlerts { get; set; }
    }

    /// <summary>
    /// Compliance trend data
    /// </summary>
    public class ComplianceTrendDto
    {
        public DateTime Date { get; set; }
        public int CompliantCount { get; set; }
        public int NonCompliantCount { get; set; }
        public decimal CompliancePercentage { get; set; }
        public int AlertsCreated { get; set; }
        public int AlertsResolved { get; set; }
    }

    /// <summary>
    /// Compliance statistics
    /// </summary>
    public class ComplianceStatisticsDto
    {
        public int TotalChecksPerformed { get; set; }
        public int TotalDocumentsProcessed { get; set; }
        public int TotalIssuesIdentified { get; set; }
        public int IssuesResolved { get; set; }

        public decimal AverageTimeToResolveHours { get; set; }
        public decimal AverageComplianceScore { get; set; }

        public int MostCommonIssue { get; set; } // Issue type count
        public string? MostCommonIssueType { get; set; }

        public int DocumentsExpiredThisMonth { get; set; }
        public int DocumentsExpiringNextMonth { get; set; }
    }

    // ============ REQUEST DTOs ============

    /// <summary>
    /// Request to check user compliance
    /// </summary>
    public class CheckComplianceRequestDto
    {
        public string? UserId { get; set; }
        public int? DepartmentId { get; set; }
        public bool RunDetailedCheck { get; set; } = true;
        public bool SendNotifications { get; set; } = false;
    }

    /// <summary>
    /// Request to approve document compliance
    /// </summary>
    public class ApproveDocumentComplianceDto
    {
        public int CheckId { get; set; }
        public string ApprovalNotes { get; set; } = string.Empty;
        public bool ApproveWithWarnings { get; set; } = false;
    }

    /// <summary>
    /// Request to set compliance deadline
    /// </summary>
    public class SetComplianceDeadlineDto
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for compliance report
    /// </summary>
    public class ComplianceReportRequestDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? DepartmentId { get; set; }
        public string? ReportType { get; set; } // "Summary", "Detailed", "Executive"
        public bool IncludeHistoricalData { get; set; } = false;
    }

    /// <summary>
    /// Request to bulk update compliance
    /// </summary>
    public class BulkComplianceUpdateDto
    {
        public List<string> UserIds { get; set; } = new();
        public string ActionType { get; set; } = string.Empty; // "Check", "Suspend", "Escalate"
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to acknowledge alert
    /// </summary>
    public class AcknowledgeAlertDto
    {
        public int AlertId { get; set; }
        public string AcknowledgmentNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to resolve alert
    /// </summary>
    public class ResolveAlertDto
    {
        public int AlertId { get; set; }
        public string ResolutionNotes { get; set; } = string.Empty;
        public bool ActionTaken { get; set; } = false;
    }
}
