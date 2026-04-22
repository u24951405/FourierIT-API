using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentFicaRule
    {
        [Key]
        [ForeignKey("DocumentType")]
        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        [Key]
        [ForeignKey("FICARule")]
        public int FICARuleId { get; set; }
        public FICARule FICARule { get; set; } = null!;
    }
}
