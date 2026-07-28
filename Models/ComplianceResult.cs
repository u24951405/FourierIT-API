using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ComplianceResult
    {
        [Key]
        public int ComplianceResultId { get; set; }

        [ForeignKey("ComplianceStatus")]
        public int ComplianceStatusId { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Scope { get; set; } = "User";

        public int TotalRequired { get; set; }
        public int MissingDocuments { get; set; }
        public int ExpiredDocuments { get; set; }
        public int RejectedDocuments { get; set; }
        public int PendingVerification { get; set; }
        public int ValidDocuments { get; set; }
        public int DuplicateDocuments { get; set; }
        public int CompliancePercentage { get; set; }

        [StringLength(50)]
        public string OverallStatus { get; set; } = "Pending";

        [StringLength(500)]
        public string? Summary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ComplianceCheck> Checks { get; set; } = new List<ComplianceCheck>();
    }
}
