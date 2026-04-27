using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ClientRiskRating
    {
        [Key]
        public int RatingId { get; set; }

        [ForeignKey("ClientEnlistment")]
        public int ClientEnlistmentId { get; set; }
        public ClientEnlistment ClientEnlistment { get; set; } = null!;

        [Required]
        public decimal TotalScore { get; set; }

        [Required]
        [StringLength(10)]
        public string RiskLevel { get; set; } = string.Empty;

        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;

        public ICollection<RiskRatingVariable> RiskRatingVariables { get; set; } = new List<RiskRatingVariable>();

        public ICollection<RiskHistory> RiskHistories { get; set; } = new List<RiskHistory>();

    }
}
