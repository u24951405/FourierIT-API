using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class InstitutionRequestedDocumentType
    {
        [ForeignKey("InstitutionEnquiryRequest")]
        public int EnquiryRequestId { get; set; }
        public InstitutionEnquiryRequest InstitutionEnquiryRequest { get; set; } = null!;

        [ForeignKey("DocumentType")]
        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        [ForeignKey("FICARule")]
        public int FICARuleId { get; set; }
        public FICARule? FICARule { get; set; }

        [Required]
        public bool isMandatory { get; set; } = true;
    }
}
