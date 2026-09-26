using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class InstitutionInvitation
    {
        [Key]
        public int InvitationId { get; set; }

        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string TokenString { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string OtpCodeHash { get; set; } = string.Empty;

        public DateTimeOffset TokenExpiryTimeStamp { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset OtpExpiryTimeStamp { get; set; } = DateTimeOffset.UtcNow;

        public bool IsRevoked { get; set; } = false;
        public bool IsUsed { get; set; } = false;

        public int OtpSendCount { get; set; } = 0;

        // Wrong codes entered for the current code; reset whenever a new code is sent.
        public int OtpFailedAttempts { get; set; } = 0;

        // When the current code was emailed, used to limit how often codes can be resent.
        public DateTimeOffset? OtpLastSentAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
