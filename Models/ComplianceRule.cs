using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class ComplianceRule
    {
        [Key]
        public int ComplianceRuleId { get; set; }

        [Required]
        [StringLength(150)]
        public string RuleName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string AppliesTo { get; set; } = "DocumentOwner";

        public int? RequiredDocumentTypeId { get; set; }

        public bool IsMandatory { get; set; } = true;

        public int? ExpiryPeriodDays { get; set; }

        public int? WarningThresholdDays { get; set; }

        [StringLength(500)]
        public string ValidationRules { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [StringLength(250)]
        public string? Description { get; set; }

        public ICollection<ComplianceRequirement> Requirements { get; set; } = new List<ComplianceRequirement>();
        public ICollection<ComplianceCheck> Checks { get; set; } = new List<ComplianceCheck>();
    }
}
