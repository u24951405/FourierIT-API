using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class DocumentAccessApproval
    {
        [Key]
        public int ApprovalId { get; set; }

        [ForeignKey("InstitutionEnquiryRequest")]
        public int EnquiryRequestId { get; set; }
        public InstitutionEnquiryRequest InstitutionEnquiryRequest { get; set; } = null!;

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        [ForeignKey("ApprovedByUserId")]
        public string ApprovedByUserId { get; set; } = string.Empty;
        public User ApprovedByUser {  get; set; } = null!;

        [Required]
        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt {  get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }
    }
}
