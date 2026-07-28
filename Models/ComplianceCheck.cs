using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ComplianceCheck
    {
        [Key]
        public int ComplianceCheckId { get; set; }

        [ForeignKey("ComplianceRule")]
        public int ComplianceRuleId { get; set; }
        public ComplianceRule ComplianceRule { get; set; } = null!;

        [ForeignKey("ComplianceResult")]
        public int? ComplianceResultId { get; set; }
        public ComplianceResult? ComplianceResult { get; set; }

        [ForeignKey("Document")]
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        [Required]
        [StringLength(50)]
        public string CheckStatus { get; set; } = "Pending";

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
