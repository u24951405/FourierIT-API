using FourierIT_API.DTOs.Compliance;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    /// <summary>
    /// Interface for compliance operations
    /// Enterprise FICA compliance system
    /// </summary>
    public interface IComplianceService
    {
        // ===== MAIN COMPLIANCE CHECKS =====
        Task<ComplianceStatus> CheckUserComplianceAsync(string userId, bool runDetailedCheck = true);
        Task<ComplianceStatus> CheckDepartmentComplianceAsync(int departmentId);
        Task<List<ComplianceStatus>> BulkCheckComplianceAsync(List<string> userIds);

        // ===== USER COMPLIANCE DETAILS =====
        Task<UserComplianceDto> GetUserComplianceDetailsAsync(string userId);
        Task<List<UserComplianceDto>> GetDepartmentUserComplianceAsync(int departmentId);

        // ===== DASHBOARD =====
        Task<ComplianceDashboardDto> GetSystemDashboardAsync();
        Task<ComplianceDashboardDto> GetDepartmentDashboardAsync(int departmentId);
        Task<ComplianceDashboardSnapshotDto> GetSystemDashboardSnapshotAsync();
        Task<ComplianceDashboardSnapshotDto> GetDepartmentDashboardSnapshotAsync(int departmentId);
        Task<ComplianceDashboardSnapshotDto> GetUserDashboardSnapshotAsync(string userId);
        Task<ComplianceStatisticsDto> GetComplianceStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

        // ===== DOCUMENT CHECKS =====
        Task<DocumentComplianceCheck> PerformDocumentCheckAsync(
            int documentId,
            int complianceStatusId);
        Task<List<DocumentComplianceIssueDto>> GetDocumentIssuesAsync(string userId);
        Task<List<MissingDocumentDto>> IdentifyMissingDocumentsAsync(string userId);

        // ===== ALERTS =====
        Task<List<ComplianceAlertDto>> GetOpenAlertsAsync(string? userId = null);
        Task<ComplianceAlertDto> CreateAlertAsync(ComplianceAlert alert);
        Task<bool> AcknowledgeAlertAsync(int alertId, string acknowledgedBy, string notes);
        Task<bool> ResolveAlertAsync(int alertId, string resolvedBy, string resolutionNotes);
        Task<List<ComplianceAlertDto>> GetOverdueAlertsAsync();

        // ===== APPROVALS =====
        Task<bool> ApproveDocumentComplianceAsync(int checkId, string approvedBy, string notes);
        Task<bool> RejectDocumentComplianceAsync(int checkId, string rejectedBy, string reason);

        // ===== DEADLINES =====
        Task<bool> SetComplianceDeadlineAsync(string userId, DateTime deadline, string reason);
        Task<List<UserComplianceSummaryDto>> GetUsersNearDeadlineAsync(int daysThreshold = 7);

        // ===== ESCALATION =====
        Task<bool> EscalateComplianceAsync(string userId, string reason);
        Task<bool> FlagForManualReviewAsync(int checkId, string reason);
        Task<List<DocumentComplianceCheck>> GetPendingManualReviewsAsync();

        // ===== REPORTING =====
        Task<byte[]> GenerateComplianceReportAsync(DateTime startDate, DateTime endDate, int? departmentId = null);
        Task<byte[]> GenerateAuditReportAsync(int complianceStatusId);

        // ===== HISTORY & AUDIT =====
        Task<List<ComplianceHistory>> GetComplianceHistoryAsync(int complianceStatusId, int limit = 50);
        Task<List<ComplianceHistoryItemDto>> GetComplianceHistoryAsync(string userId, int limit = 50);
        Task<List<ComplianceAuditLog>> GetAuditLogsAsync(int complianceStatusId, int limit = 100);
        Task<List<ComplianceRuleDto>> GetComplianceRulesAsync();

        // ===== BULK OPERATIONS =====
        Task<int> BulkApproveDocumentsAsync(List<int> checkIds, string approvedBy);
        Task<int> BulkRejectDocumentsAsync(List<int> checkIds, string rejectedBy, string reason);

        // ===== CONFIGURATION =====
        Task<bool> ConfigureComplianceRulesAsync(string complianceCategory, Dictionary<string, object> rules);
        Task<Dictionary<string, object>> GetComplianceRulesAsync(string complianceCategory);

        // ===== NOTIFICATIONS =====
        Task<bool> SendNonComplianceNotificationAsync(string userId);
        Task<bool> SendDeadlineReminderAsync(string userId);
        Task<bool> ProcessExpiredDocumentComplianceAsync();

        // ===== UTILITIES =====
        Task<int> CalculateComplianceScoreAsync(string userId);
        Task<int> AssessRiskLevelAsync(string userId);
        Task<bool> UpdateComplianceStatusAsync(int statusId);
    }
}
