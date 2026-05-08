using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AccessList
    {

        [ForeignKey("InstitutionEnquiryRequest")]
        public int EnquiryRequestId { get; set; }
        public InstitutionEnquiryRequest InstitutionEnquiryRequest { get; set; } = null!;


        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;


        [ForeignKey("EnquiryFlag")]
        public int EnquiryId { get; set; }
        public EnquiryFlag EnquiryFlag { get; set; } = null!;
    }
}
