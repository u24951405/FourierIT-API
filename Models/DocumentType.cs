using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class DocumentType
    {
        [Key]
        public int DocumentTypeId { get; set; }

        [Required]
        public string TypeName { get; set; } = string.Empty;
        
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        public ICollection<DocumentFicaRule> DocumentFicaRules { get; set; } = new List<DocumentFicaRule>();
    }
}
