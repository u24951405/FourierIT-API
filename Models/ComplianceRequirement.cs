using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ComplianceRequirement
    {
        [Key]
        public int ComplianceRequirementId { get; set; }

        [ForeignKey("ComplianceRule")]
        public int ComplianceRuleId { get; set; }
        public ComplianceRule ComplianceRule { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string RequirementName { get; set; } = string.Empty;

        public int? DocumentTypeId { get; set; }

        public bool IsMandatory { get; set; } = true;

        public int? ExpiryPeriodDays { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }
    }
}
