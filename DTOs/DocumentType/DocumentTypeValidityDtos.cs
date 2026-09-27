using FourierIT_API.Models;

namespace FourierIT_API.DTOs.DocumentType;

public class DocumentTypeSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ValidityMonths { get; set; }
    public ValidityBasis ValidityBasis { get; set; }
    public int WarningDays { get; set; }
    public int DocumentCount { get; set; }
}

public class DocumentTypeValidityUpdateRequest
{
    public int ValidityMonths { get; set; }
    public ValidityBasis ValidityBasis { get; set; }
    public int WarningDays { get; set; }
}

public class DocumentTypeValiditySummaryDto
{
    public int AffectedDocuments { get; set; }
    public int BecomeExpired { get; set; }
    public int NoLongerExpired { get; set; }
    public int MissingSourceDate { get; set; }
    public bool ComplianceRecalculationFailed { get; set; }
}
