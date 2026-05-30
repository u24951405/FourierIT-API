using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class InstitutionEnquiryRequest
    {
        [Key]
        public int EnquiryRequestId { get; set; }

        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        [ForeignKey("TargetUser")]
        public string TargetUserId { get; set; } = string.Empty;
        public User TargetUser { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string Status { get; set; }  = "Pending";

        [Required]
        [StringLength(500)]
        public string PurposeNote { get; set; } = string.Empty;

        public DateTimeOffset RequestDate { get; set; } = DateTimeOffset.UtcNow;

        public AccessToken AccessToken { get; set; } = null!;

        public DateTime? RespondedAt { get; set; }

        [StringLength(500)]
        public string? UserResponseNote { get; set; }

        public ICollection<AccessList> AccessLists { get; set; } = new List<AccessList>();
        public ICollection<InstitutionRequestedDocumentType> RequestedDocumentTypes { get; set; } = new List<InstitutionRequestedDocumentType>();
    }
}
