using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class UserSecurityQuestion
    {
        [Key]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        [Key]
        [ForeignKey("SecurityQuestion")]
        public int SecurityQuestionId { get; set; }
        public SecurityQuestion SecurityQuestion { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string EncryptedAnswer { get; set; } = string.Empty;
    }
}
