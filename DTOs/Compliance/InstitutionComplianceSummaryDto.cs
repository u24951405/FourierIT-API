namespace FourierIT_API.DTOs.Compliance;

public sealed class InstitutionComplianceSummaryDto
{
    public int InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string ComplianceStatus { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int RequiredDocumentTypeCount { get; set; }
    public int SubmittedOrApprovedDocumentTypeCount { get; set; }
    public int MissingDocumentTypeCount { get; set; }
    public string MissingDocumentTypes { get; set; } = string.Empty;
}
