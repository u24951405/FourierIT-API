namespace FourierIT_API.DTOs.Reports
{
    public class AdHocReportRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public List<string> FocusAreas { get; set; } = new();
        public string ExportFormat { get; set; } = "PDF";
    }

    public class AdHocReportSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public long SizeKb { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class AdHocReportDataDto
    {
        public int ReportId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public DateTime DateGenerated { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public List<string> FocusAreas { get; set; } = new();
        public List<AdHocComplianceResultDto> ComplianceResults { get; set; } = new();
        public List<AdHocDocumentResultDto> DocumentResults { get; set; } = new();
        public List<AdHocSecurityResultDto> SecurityResults { get; set; } = new();
        public List<AdHocDistributionResultDto> DistributionResults { get; set; } = new();
        public List<AdHocUploadVolumeResultDto> UploadVolumeResults { get; set; } = new();
        public AdHocStorageResultDto? StorageResult { get; set; }
    }

    public class AdHocComplianceResultDto
    {
        public int StatusId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string OverallStatus { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public decimal CompliancePercentage { get; set; }
        public decimal OverallRiskScore { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class AdHocDocumentResultDto
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public long FileSizeBytes { get; set; }
        public bool Certified { get; set; }
    }

    public class AdHocSecurityResultDto
    {
        public int AuditLogId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
    }

    public class AdHocDistributionResultDto
    {
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class AdHocUploadVolumeResultDto
    {
        public DateTime UploadDate { get; set; }
        public int UploadCount { get; set; }
    }

    public class AdHocStorageResultDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public int DocumentCount { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class DocumentOwnerComplianceReportRowDto
    {
        public string DocumentOwner { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string ComplianceStatus { get; set; } = string.Empty;
        public int UploadedDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public DateTime? LastUploadDate { get; set; }
        public decimal CompliancePercentage { get; set; }
        public string RiskRating { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string Department { get; set; } = string.Empty;
        public int InstitutionId { get; set; }
        public string Institution { get; set; } = string.Empty;
        public string[] MissingDocumentNames { get; set; } = Array.Empty<string>();
    }

    public class InstitutionDocumentRequestReportRowDto
    {
        public int RequestId { get; set; }
        public string Institution { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string RecipientType { get; set; } = string.Empty;
        public DateTimeOffset RequestDate { get; set; }
        public DateTimeOffset? SubmissionDeadline { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string RequestJustification { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? ApprovalDenialDate { get; set; }
        public string[] RequestedDocuments { get; set; } = Array.Empty<string>();
        public int InstitutionId { get; set; }
        public int? DepartmentId { get; set; }
    }

    public class DepartmentComplianceReportRowDto
    {
        public int DepartmentId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string DepartmentAdmin { get; set; } = string.Empty;
        public int RequiredDocuments { get; set; }
        public int UploadedDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public decimal CompliancePercentage { get; set; }
        public string RiskRating { get; set; } = string.Empty;
        public int InstitutionId { get; set; }
        public string Institution { get; set; } = string.Empty;
    }

    public class InstitutionRequestControlBreakGroupDto
    {
        public int InstitutionId { get; set; }
        public string Institution { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int Approved { get; set; }
        public int Pending { get; set; }
        public int Denied { get; set; }
        public List<InstitutionDocumentRequestReportRowDto> Requests { get; set; } = new();
    }

    public class DepartmentComplianceControlBreakDetailDto
    {
        public string DocumentOwner { get; set; } = string.Empty;
        public string ComplianceStatus { get; set; } = string.Empty;
        public int UploadedDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public decimal CompliancePercentage { get; set; }
        public string RiskRating { get; set; } = string.Empty;
    }

    public class DepartmentComplianceControlBreakGroupDto
    {
        public int DepartmentId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string DepartmentAdmin { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public int RequiredDocuments { get; set; }
        public int UploadedDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public decimal CompliancePercentage { get; set; }
        public List<DepartmentComplianceControlBreakDetailDto> Details { get; set; } = new();
    }

    public class DepartmentDocumentInventoryReportRowDto
    {
        public int DepartmentId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string DepartmentAdmin { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public string RequiredDocument { get; set; } = string.Empty;
        public bool IsUploaded { get; set; }
        public DateTime? UploadDate { get; set; }
        public int RequiredDocuments { get; set; }
        public int UploadedDocuments { get; set; }
        public int MissingDocuments { get; set; }
        public decimal CompliancePercentage { get; set; }
        public string RiskRating { get; set; } = string.Empty;
    }

    public class InstitutionAccessHistoryReportRowDto
    {
        public int RequestId { get; set; }
        public string Institution { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string RecipientType { get; set; } = string.Empty;
        public DateTimeOffset? AccessGrantedDate { get; set; }
        public DateTimeOffset? AccessExpiry { get; set; }
        public string AccessStatus { get; set; } = string.Empty;
        public string[] DocumentsAccessed { get; set; } = Array.Empty<string>();
    }

    public class ExpiringDocumentsReportRowDto
    {
        public string Owner { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTimeOffset ExpiryDate { get; set; }
        public bool NeverExpires { get; set; }
        public int DaysRemaining { get; set; }
    }

    public class OutstandingComplianceReportRowDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public int MissingDocuments { get; set; }
        public decimal CompliancePercentage { get; set; }
        public string RiskRating { get; set; } = string.Empty;
        public string[] OutstandingRequirements { get; set; } = Array.Empty<string>();
    }

    public class ActivityReportDto
    {
        public string ReportId { get; set; } = string.Empty;
        public DateTime DateGenerated { get; set; } = DateTime.UtcNow;
        public string DocumentOwner { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public int ActiveDocuments { get; set; }
        public int InactiveDocuments { get; set; }
        public int TotalDocuments { get; set; }
        public List<MonthlyDistributionCategoryDto> DistributionByCategory { get; set; } = new();
        public List<DocumentInventoryItemDto> Inventory { get; set; } = new();
        public List<VaultAccessLogEntryDto> VaultAccessLog { get; set; } = new();
        public List<ClientRelationshipDto> ClientRelationships { get; set; } = new();
    }

    public class DocumentInventoryItemDto
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryColor { get; set; } = string.Empty;
        public string UploadDate { get; set; } = string.Empty;
        public string? ExpiryDate { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
    }

    public class VaultAccessLogEntryDto
    {
        public string Timestamp { get; set; } = string.Empty;
        public string AccessorName { get; set; } = string.Empty;
        public string AccessorRole { get; set; } = string.Empty;
        public string ActionReason { get; set; } = string.Empty;
        public string Organisation { get; set; } = string.Empty;
    }

    public class ClientRelationshipDto
    {
        public string Organisation { get; set; } = string.Empty;
        public int DocumentsShared { get; set; }
        public string Status { get; set; } = string.Empty;
    }

}