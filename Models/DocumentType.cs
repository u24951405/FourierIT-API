using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class DocumentType
    {
        [Key]
        public int DocumentTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TypeName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        public int ValidityMonths { get; set; } = 3;


        [Required]
        public ValidityBasis ValidityBasis { get; set; } = ValidityBasis.CertificationDate;

        [Required]
        public int WarningDays { get; set; } = 30;

        public ICollection<Document> Documents { get; set; } = new List<Document>();

        public ICollection<DocumentFicaRule> DocumentFicaRules { get; set; } = new List<DocumentFicaRule>();
    }
}
