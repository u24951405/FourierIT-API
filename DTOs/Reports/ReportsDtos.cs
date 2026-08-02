namespace FourierIT_API.DTOs.Reports
{
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
}