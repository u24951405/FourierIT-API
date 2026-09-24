namespace FourierIT_API.DTOs.Compliance
{
    public class ComplianceDashboardSnapshotDto
    {
        public string Scope { get; set; } = string.Empty;
        public string? ScopeId { get; set; }
        public DateTime? LastChecked { get; set; }
        public DocumentsOverviewDto DocumentsOverview { get; set; } = new();
        public RiskSummaryDto Risk { get; set; } = new();
        public List<ExpiringDocumentDto> ExpiringDocuments { get; set; } = new();
    }

    public class DocumentsOverviewDto
    {
        public int Total { get; set; }
        public int Compliant { get; set; }
        public int Expired { get; set; }
        public int NonCompliant { get; set; }
        public int Unchecked { get; set; }
    }

    public class RiskSummaryDto
    {
        public decimal ComplianceScore { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
    }

    public class ExpiringDocumentDto
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTimeOffset ExpiryDate { get; set; }
        public bool NeverExpires { get; set; }
        public int DaysRemaining { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
