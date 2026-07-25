using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    [Table("InstitutionSessionTokens")]
    public class InstitutionSessionToken
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public int InstitutionId { get; set; }

        [ForeignKey(nameof(InstitutionId))]
        public Institution Institution { get; set; } = null!;

        [Required]
        public string TokenString { get; set; } = string.Empty;

        [Required]
        public DateTime IssuedAt { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }

        public DateTime? RevokedAt { get; set; }
    }
}
