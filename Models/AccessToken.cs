using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class AccessToken
    {
        [Key]
        public int TokenId { get; set; }

        [ForeignKey("institutionEnquiryRequest")]
        public int EnquiryRequestId { get; set; }
        public InstitutionEnquiryRequest institutionEnquiryRequest { get; set; } = null!;

        public string TokenString { get; set; } = string.Empty;

        public DateTime ExpiryTimeStamp { get; set; } = new DateTime();

        public bool IsRevoked { get; set; } = false;

        public EnquirySession EnquirySession { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}
