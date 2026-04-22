using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace FourierIT_API.Models
{
    public class EnquiryComment
    {
        [Key]
        public int EnquiryCommentId { get; set; }

        [ForeignKey("EnquiryFlag")]
        public int EnquiryFlagId { get; set; }
        public EnquiryFlag EnquiryFlag { get; set; } = null!;

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public string MessageText { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
