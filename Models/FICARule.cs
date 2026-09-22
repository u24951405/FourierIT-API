using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class FICARule
    {
        [Key]
        public int RuleId { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Obsolete("Document validity is configured on DocumentType")]
        public int ValidityMonths { get; set; } = 0;

        public ICollection<DocumentFicaRule> DocumentFicaRules { get; set; } = new List<DocumentFicaRule>();

        public ICollection<FICARuleHistory> FICARuleHistories { get; set; } = new List<FICARuleHistory>();
    }
}
