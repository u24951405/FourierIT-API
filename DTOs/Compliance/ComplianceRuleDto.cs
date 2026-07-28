namespace FourierIT_API.DTOs.Compliance
{
    public class ComplianceRuleDto
    {
        public int ComplianceRuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string AppliesTo { get; set; } = "DocumentOwner";
        public bool IsMandatory { get; set; }
        public string ValidationRules { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Description { get; set; }
    }

    public class ComplianceHistoryItemDto
    {
        public string Status { get; set; } = string.Empty;
        public int CompliancePercentage { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}
