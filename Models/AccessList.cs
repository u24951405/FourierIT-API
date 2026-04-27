using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AccessList
    {
        [Key]
        [ForeignKey("InstitutionEnquiryRequest")]
        public int EnquiryRequestId { get; set; }
        public InstitutionEnquiryRequest InstitutionEnquiryRequest { get; set; } = null!;

        [Key]
        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        [Key]
        [ForeignKey("EnquiryFlag")]
        public int EnquiryId { get; set; }
        public EnquiryFlag EnquiryFlag { get; set; } = null!;
    }
}
