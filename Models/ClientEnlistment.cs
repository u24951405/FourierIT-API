using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ClientEnlistment
    {
        [Key]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Key]
        [ForeignKey("Institution")]
        public int InstitutionId { get; set; }
        public Institution Institution { get; set; } = null!;

        [Key]
        public int ClientEnlistmentId { get; set; }

        [Required]
        public DateTimeOffset EnlistmentDate { get; set; } = DateTimeOffset.Now;

        [Required]
        [StringLength(20)]
        public string EnlistmentStatus { get; set; } = string.Empty;

        public ClientRiskRating ClientRiskRating { get; set; } = null!;
    }
}
