using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class InstitutionMembers
    {
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        [Key]
        public int MembersId { get; set; }

        [Required]
        public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
    }
}
