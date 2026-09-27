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

        [Required]
        [MaxLength(255)]
        public string TokenString { get; set; } = string.Empty;

        public DateTimeOffset ExpiryTimeStamp { get; set; } = DateTimeOffset.Now;

        public bool IsRevoked { get; set; } = false;

        /// <summary>When the institution was emailed that this access is about to end (cleared when access is extended).</summary>
        public DateTimeOffset? ExpiryReminderSentAt { get; set; }

        public EnquirySession EnquirySession { get; set; } = null!;

        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}
